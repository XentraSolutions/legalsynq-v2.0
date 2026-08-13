export const TOTAL_STEPS = 2;

// Every step's "Continue"/"Back" moves the URL to the target step's real
// route — this is the one place that knows that URL shape.
export function goToStep(
  router: { push: (href: string) => void },
  lienId: string,
  step: number,
) {
  router.push(`/selling/portfolio/lien/${lienId}/sell/step-${step}`);
}
