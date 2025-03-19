
const unitConversions = {
	mass: {
		g: 1,
		oz: 28.3495,
		lb: 453.592
	},
	volume: {
		ml: 1,
		cup: 236.588,
		tbsp: 14.7868
	}
};

// export default function convert(amount: number, from: string, to: string) {
// 	const fromUnit = unitConversions[from as keyof typeof unitConversions];
// 	const toUnit = unitConversions[to as keyof typeof unitConversions];

// 	return amount * (toUnit / fromUnit);
// }

