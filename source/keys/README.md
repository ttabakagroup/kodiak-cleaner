# Supporter Keys

| File | What it is | In git? |
|------|------------|---------|
| `supporter-public.pem` | Ted's public key. Built into the app to check supporter codes. Created by `make-key`. | Yes, once it exists |
| `supporter-private.pem` | Ted's private key. Signs supporter codes. **Never share it, never commit it, keep a backup.** | No (ignored) |
| `supporter-test-public.pem` | Test key used until Ted's key exists. Release builds refuse to build with it. | Yes |
| `withdrawn-codes.txt` | IDs of codes that should stop working. Shipped inside the app. | Yes |

Make Ted's key pair (run once, on Ted's own PC):

```powershell
dotnet run --project tools/SupporterCodes -- make-key
```

Issue a code after someone supports Kodiak Cleaner on Ko-fi:

```powershell
dotnet run --project tools/SupporterCodes -- make-code "Jane Doe"
```
