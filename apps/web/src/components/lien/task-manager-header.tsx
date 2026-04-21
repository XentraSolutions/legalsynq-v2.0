'use client';

type ViewMode = 'board' | 'list';

interface TaskManagerHeaderProps {
  title: string;
  taskCount: number;
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  onNewTask: () => void;
}

export function TaskManagerHeader({
  title,
  taskCount,
  viewMode,
  onViewModeChange,
  onNewTask,
}: TaskManagerHeaderProps) {
  return (
    <div className="flex items-center justify-between gap-3">
      <div className="flex items-center gap-2">
        <span className="text-base font-semibold text-gray-800">{title}</span>
        <span className="text-xs bg-gray-100 text-gray-500 rounded-full px-2 py-0.5 font-medium tabular-nums">
          {taskCount} task{taskCount !== 1 ? 's' : ''}
        </span>
      </div>
      <div className="flex items-center gap-2">
        <div className="flex items-center border border-gray-200 rounded-lg overflow-hidden">
          <button
            onClick={() => onViewModeChange('board')}
            className={`px-2.5 py-1 text-xs flex items-center gap-1 transition-colors ${
              viewMode === 'board' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'
            }`}
          >
            <i className="ri-layout-column-line" /> Board
          </button>
          <button
            onClick={() => onViewModeChange('list')}
            className={`px-2.5 py-1 text-xs flex items-center gap-1 transition-colors ${
              viewMode === 'list' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'
            }`}
          >
            <i className="ri-list-unordered" /> List
          </button>
        </div>
        <button
          onClick={onNewTask}
          className="flex items-center gap-1 text-xs font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-3 py-1.5 transition-colors"
        >
          <i className="ri-add-line" /> New Task
        </button>
      </div>
    </div>
  );
}
