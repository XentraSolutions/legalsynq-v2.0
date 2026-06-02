'use client';

import { useState, type ReactNode } from 'react';
import { motion } from 'framer-motion';

export type PanelMode = 'split' | 'left-expanded' | 'right-expanded';

interface LayoutSplitProps {
  left: ReactNode;
  right: ReactNode;
  leftTitle?: ReactNode;
  rightTitle?: ReactNode;

  defaultMode?: 'split' | 'left' | 'right';
  mode?: PanelMode;
  onModeChange?: (mode: PanelMode) => void;
  className?: string;
}

const MODE_MAP: Record<string, PanelMode> = {
  split: 'split',
  left: 'left-expanded',
  right: 'right-expanded',
};

export function LayoutSplit({
  left,
  right,
  leftTitle,
  rightTitle,
  defaultMode = 'split',
  mode: controlledMode,
  onModeChange,
  className,
}: LayoutSplitProps) {
  const [internalMode, setInternalMode] = useState<PanelMode>(
    MODE_MAP[defaultMode] ?? 'split'
  );

  const mode = controlledMode ?? internalMode;

  const setMode = (m: PanelMode) => {
    if (onModeChange) onModeChange(m);
    else setInternalMode(m);
  };

  const isLeft = mode === 'left-expanded';
  const isRight = mode === 'right-expanded';
  const isSplit = mode === 'split';

  const toggleLeft = () => setMode(isLeft ? 'split' : 'left-expanded');
  const toggleRight = () => setMode(isRight ? 'split' : 'right-expanded');

  const leftFlex = isLeft ? 1 : isRight ? 0.2 : 1;
  const rightFlex = isRight ? 1 : isLeft ? 0.2 : 0.42;

  const leftBtnIcon = isLeft ? 'left' : 'right';
  const rightBtnIcon = isRight ? 'right' : 'left';

  const leftCollapsed = isRight;   // left is minimized when right expanded
  const rightCollapsed = isLeft;   // right is minimized when left expanded

  return (
    <div className={`flex w-full items-stretch gap-0 ${className ?? ''}`}>
      {/* LEFT */}
      <motion.div
        animate={{ flex: leftFlex }}
        transition={{ type: 'spring', stiffness: 220, damping: 28 }}
        className="min-w-0 relative overflow-hidden"
      >
        {/* CONTENT */}
        <div className={`transition-opacity duration-200`}>
          {left}
        </div>
      </motion.div>

      {/* CONTROLS */}
      <div className="flex flex-col items-center justify-start pt-1 gap-1 shrink-0 mx-1">
        <button
          onClick={toggleLeft}
          title={isSplit ? 'Expand left panel' : 'Restore split view'}
          className={`w-7 h-7 flex items-center justify-center rounded-md border transition-colors ${
            isLeft
              ? 'border-primary bg-primary/10 text-primary'
              : 'border-gray-200 bg-white text-gray-400 hover:text-gray-600'
          }`}
        >
          <i className={`ri-arrow-${leftBtnIcon}-s-line text-sm`} />
        </button>

        <div className="w-px h-4 bg-gray-200" />

        <button
          onClick={toggleRight}
          title={isSplit ? 'Expand right panel' : 'Restore split view'}
          className={`w-7 h-7 flex items-center justify-center rounded-md border transition-colors ${
            isRight
              ? 'border-primary bg-primary/10 text-primary'
              : 'border-gray-200 bg-white text-gray-400 hover:text-gray-600'
          }`}
        >
          <i className={`ri-arrow-${rightBtnIcon}-s-line text-sm`} />
        </button>
      </div>

      {/* RIGHT */}
      <motion.div
        animate={{ flex: rightFlex }}
        transition={{ type: 'spring', stiffness: 220, damping: 28 }}
        className="min-w-0 relative overflow-hidden"
      >
        {/* CONTENT */}
        <div className={`transition-opacity duration-200`}>
          {right}
        </div>
      </motion.div>
    </div>
  );
}