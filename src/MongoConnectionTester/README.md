# Mongo connection tester

Console app which connects to a mongodb cluster, and tracks connections.
The app idles, and does not do any operations.

## Usage:

- Create an Atlas db with X509 auth.
- Add appsettings.local.json with your connection string and certificate data:

```json
{
    "MongoDb": {
        "ConnectionString":"mongodb+srv://somedb.some-hash.mongodb.net/?ssl=true&authSource=%24external&authMechanism=MONGODB-X509&your-options-here",
        "Certificate": {
            "Thumbprint": "",
            "PfxName": "/path/to/client-certificate.pfx",
            "PfxPassword": "verysecret"
        }
    }
}
```

- `dotnet run`