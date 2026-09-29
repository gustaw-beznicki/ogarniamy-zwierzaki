# Tests and checks

```bash
dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj   # needs Docker running
dotnet build services/api/ogarniamy-zwierzaki-api.csproj
npm run build --prefix apps/web
infra/deploy.sh lint                                                  # needs the Azure CLI with Bicep
```

The tests boot the real API against a throwaway PostgreSQL 17 container, so no mocks or local database are involved. Pull-request CI runs all of the above plus an infrastructure `what-if`.
