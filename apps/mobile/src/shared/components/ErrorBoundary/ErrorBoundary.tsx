import type { ErrorInfo, ReactNode } from 'react';
import { Component } from 'react';
import { Ionicons } from '@expo/vector-icons';

import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorTrackingService } from '@/shared/services/ErrorTracking';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = {
    hasError: false,
  };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    ErrorTrackingService.captureException(error, { componentStack: errorInfo.componentStack });
  }

  reset = (): void => {
    this.setState({ hasError: false });
  };

  render() {
    if (this.state.hasError) {
      return (
        <EmptyState
          actionLabel="Try Again"
          description="Please try again or contact support if the issue continues."
          icon={<Ionicons color="#d97706" name="warning" size={64} />}
          title="Something went wrong"
          onAction={this.reset}
        />
      );
    }

    return this.props.children;
  }
}
