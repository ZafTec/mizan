# Changelog

## 3.0.0 (2026-09-07)


### Features

* **ai:** consent and usage in settings, chat off the Pro wall ([80b82c6](https://github.com/ZafTec/mizan/commit/80b82c63090be4d0b82d48089b8a88faf103774d))
* **ai:** the platform - provider, meter, consent, one access policy ([060ead2](https://github.com/ZafTec/mizan/commit/060ead249a19fee173e1fbceee3b60a4b3a82ea1))
* **auth:** cut the frontend over to backend identity ([e71fa7b](https://github.com/ZafTec/mizan/commit/e71fa7bc6234d7698b5fad8bf4576dad6d417237))
* **auth:** the backend owns identity, and one migration owns the schema ([81f103e](https://github.com/ZafTec/mizan/commit/81f103e878a690012fb86c5ed6324cde0439186e))
* **foods:** users own the foods they create ([f9b970a](https://github.com/ZafTec/mizan/commit/f9b970a38c3dd1bf746e52ed498a8166db5305c6))
* **nav:** phase 2 - replace the flat 21-item nav with a three-tier spine ([0f7c5f1](https://github.com/ZafTec/mizan/commit/0f7c5f17839e0a82e9ccac4eb8165ff56f47c170))
* **nav:** tier 2 contextual surfaces ([fe1cda8](https://github.com/ZafTec/mizan/commit/fe1cda861eeea7f06725c3e226ba05591e112532))
* **recipes:** mixed meals, owned foods, and refusing to guess macros ([e9fe3af](https://github.com/ZafTec/mizan/commit/e9fe3af04b4ef5a0172377445e87e61c4891aaef))
* **recipes:** preparations and food ownership ([671d00c](https://github.com/ZafTec/mizan/commit/671d00cab4618b52ed6373f48e2af51a99b3a5c8))
* **recipes:** promote a logged meal into a recipe ([56c5d70](https://github.com/ZafTec/mizan/commit/56c5d70c0228f0020a47b81ee60d138cb78a6f23))
* **storage:** S3 behind IStorageService, Cloudinary gone ([21d00e4](https://github.com/ZafTec/mizan/commit/21d00e40684caf98c8c0c735109293c46b2ed4d0))


### Bug Fixes

* configure Azure AI, SMTP relay, and semantic releases ([777842a](https://github.com/ZafTec/mizan/commit/777842a72508d34a9a7a721a34776ded312ff132))
* **db:** add the migration for the removed household_members column ([f0f8a11](https://github.com/ZafTec/mizan/commit/f0f8a1137035c783ca0b2e9590025a1771eeb149))
* finish the recipe collapse in the truncate list and the MCP tools ([6376cf6](https://github.com/ZafTec/mizan/commit/6376cf6a85383916acbdd4ece2a4d35c15008283))
* **foods:** restore API-key auth on the food endpoints ([af91643](https://github.com/ZafTec/mizan/commit/af91643e6d406e5a4d75c9fd4bca5eeb5efd6085))
* **route-audit:** escape regex metacharacters when matching dynamic routes ([56ffc41](https://github.com/ZafTec/mizan/commit/56ffc41ff8e6251ce333d74e02b7ec1bfbef01f5))
* **security:** address the CodeQL findings properly ([99a4eb6](https://github.com/ZafTec/mizan/commit/99a4eb6cd7e176ff3364c0b736c905fde0f49dfb))
* **security:** close the redirect and log-exposure paths CodeQL flagged ([64e383e](https://github.com/ZafTec/mizan/commit/64e383eff0104e44cefac47dfafbbe3250037b08))
* **trainers:** the client owns the data-sharing grants, not the trainer ([18b2214](https://github.com/ZafTec/mizan/commit/18b2214a43bf052ec86a5345bb92293790d3781e))
