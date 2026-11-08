namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// How the API authenticates to the Azure OpenAI embedding deployment. Selected by the Embedding:Auth setting.
public enum EmbeddingAuthMode
{
    // Embedding is off; the API never calls the provider. The default, so local runs and tests never call Azure.
    Disabled,

    // An Entra token of the App Service system-assigned managed identity. No keys: the resource disables local auth.
    AzureManagedIdentity,

    // The developer's own `az login` identity, for local runs. Refused outside the Development environment.
    DeveloperCredential,
}
