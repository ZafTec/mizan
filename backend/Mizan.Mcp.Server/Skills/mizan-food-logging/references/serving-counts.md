# Converting an amount to servings

Nutrition in Mizan is stored per 100 g. Each food also has a serving size, for example 100 g or 1 slice. `servings` multiplies that serving size.

| Person says | Food serving size | servings |
| - | - | - |
| 150 g of Greek yogurt | 100 g | 1.5 |
| 2 eggs | 1 egg (50 g) | 2 |
| a 30 g handful of almonds | 28 g | about 1.07, round to 1.1 |
| half a cup of oats (40 g) | 40 g | 1 |
| 2 slices of bread | 1 slice | 2 |

Rules:

- Use the unit in `servingUnit`. If the person gives grams and the serving is a piece, ask how much one piece weighs, or use a known weight and say so.
- Round to a sensible precision (one or two decimals). Do not report false precision back to the person.
- When the amount is vague ("a bit of", "some"), ask or offer a common portion and let the person confirm.
