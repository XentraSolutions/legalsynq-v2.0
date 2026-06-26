import { Button, type ButtonProps } from '@/shared/components/Button';

export function QuickActionButton(props: ButtonProps) {
  return <Button className="flex-1" size="lg" {...props} />;
}
