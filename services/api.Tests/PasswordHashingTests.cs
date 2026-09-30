using System.Buffers.Binary;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ogarniamy_zwierzaki_api.Auth;

namespace ogarniamy_zwierzaki_api.Tests;

public sealed class PasswordHashingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int ExpectedIterations = 220_000;

    [Fact]
    public async Task New_passwords_are_hashed_with_the_configured_work_factor()
    {
        var (_, email) = await factory.CreateSignedInClientAsync();

        Assert.Equal(ExpectedIterations, await StoredIterationCountAsync(email));
    }

    [Fact]
    public async Task A_weaker_stored_hash_is_rehashed_at_the_next_sign_in()
    {
        var (_, email) = await factory.CreateSignedInClientAsync();
        await ReplaceHashAsync(email, iterations: 100_000);
        Assert.Equal(100_000, await StoredIterationCountAsync(email));

        using var client = factory.CreateClient();
        using var response = await client.LoginAsync(email, TestAccounts.ValidPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(ExpectedIterations, await StoredIterationCountAsync(email));
    }

    private async Task ReplaceHashAsync(string email, int iterations)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("User not found.");
        var weakerHasher = new PasswordHasher<AppUser>(Options.Create(new PasswordHasherOptions { IterationCount = iterations }));
        user.PasswordHash = weakerHasher.HashPassword(user, TestAccounts.ValidPassword);
        var result = await users.UpdateAsync(user);
        Assert.True(result.Succeeded);
    }

    // Identity V3 format: 1-byte marker, then PRF, iteration count and salt length as big-endian 32-bit integers.
    private async Task<int> StoredIterationCountAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("User not found.");
        var hash = Convert.FromBase64String(user.PasswordHash ?? throw new InvalidOperationException("No password hash."));
        Assert.Equal(0x01, hash[0]);
        return (int)BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(5, 4));
    }
}
