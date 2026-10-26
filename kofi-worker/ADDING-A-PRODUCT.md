# Adding a Product to the Ko-fi Webhook

Every app Ted sells or takes support for on Ko-fi goes through the one Worker at `kofi.tedtabaka.com`. Each app gets:

- **its own key pair**, so a code for one app never unlocks another;
- **its own Ko-fi Shop item**, which is its own page on Ko-fi with its own link and price, and which tells the Worker which app a payment is for;
- **its own entry in `PRODUCTS`** in `worker.js`.

Plain donations (the coffee button) stay with Kodiak Cleaner.

## 1. Make the app's key pair (your computer, once per app)

From the `kofi-worker` folder:

```
node tools/make-key.mjs <app-id>
```

Use a short lowercase id such as `coclear`. This needs Node.js. Kodiak Cleaner's `make-key` tool works too, but it only writes Kodiak's own key file.

You get two files in `keys\`, a folder git ignores:
- `<app-id>-private.pem`: secret. Back it up, and paste it only into the Worker (step 4).
- `<app-id>-public.pem`: safe to share. It goes into the app (step 5).

The tool refuses to replace an existing key, because a new key would stop every code already issued for that app.

## 2. Make the app's Ko-fi Shop item (its own page)

1. On ko-fi.com, go to Shop > Add Item, then choose Digital Download (or Commission/Other) and name it, for example "CoClear Supporter".
2. Set the price. Use a fixed price, or "pay what you want" with a minimum.
3. Publish it, then open its page. Its link ends in a code, such as `ko-fi.com/s/abc123xyz0`. That code (`abc123xyz0`) is the item's link code.
4. Use this item's link wherever the app says "Support on Ko-fi", so supporters land on the right page.

## 3. Add the app to `PRODUCTS` in `worker.js`

Keep the list A to Z by name. For example:

```js
{
  id: "coclear",
  name: "CoClear",
  keySecret: "COCLEAR_PRIVATE_KEY",
  from: "CoClear <supporters@tedtabaka.com>",
  shopCodes: ["abc123xyz0"],
  tiers: [],
  min: 5,
  howTo: "Open CoClear, go to Settings > Supporter, paste the code and click Apply. Your code covers up to 10 of your computers.",
  contact: "https://www.tedtabaka.com/contact/",
},
```

- `from` must use a domain verified in Resend. Verify tedtabaka.com once in Resend, and every app can send from it.
- `min` is optional. Without it, the app uses `SUPPORTER_MIN` (default $10).
- Only Kodiak Cleaner has `default: true`.
- `npm test` checks that every entry is complete and that no id, secret or shop code is used twice.

## 4. Add the key to the Worker and deploy

1. In Cloudflare, go to Workers & Pages > kofi-webhook > Settings > Variables and Secrets > Add. Set the type to Secret, the name to the `keySecret` from step 3 (e.g. `COCLEAR_PRIVATE_KEY`), and the value to the whole private .pem. Save.
2. Go to Edit code, select all, paste in the new `worker.js`, and click Deploy.

## 5. Make the app check codes

Copy the checker for the app's language from `verifiers/` into the app, along with `<app-id>-public.pem`. Every checker works offline:

| App type | File |
|----------|------|
| Browser extension, web app, Electron, Node | `supporter-code.mjs` |
| Python | `supporter_code.py` (needs `cryptography`) |
| Swift (Mac, iPhone) | `SupporterCode.swift` |
| C# / .NET | `SupporterCode.cs` |

Each checker returns valid, empty, malformed, bad signature, withdrawn or unverifiable, plus the name on the code. Ship a withdrawn-codes list with the app so a refunded or leaked code can be shut off.

## 6. Test

Buy the item yourself, or have a friend buy it. Ko-fi's Send Test always comes in as a Kodiak Cleaner order, so it can't test a new app.

1. Your note should name the right product and carry a code.
2. Paste that code into the app. It should say the code is active.
3. Refund yourself on Ko-fi if you like, then add the code ID from the note to the app's withdrawn list.
