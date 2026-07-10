import { ActionMenu } from "@/components/lien/action-menu";

type ImportCompleteComponentProps = {
  totalCount: number;
  onRestart: () => void;
};
export default function ImportCompleteComponent({
  totalCount,
  onRestart,
}: ImportCompleteComponentProps) {
  return (
    <div className="text-center py-8">
      <i className="ri-checkbox-circle-line text-5xl text-green-500 mb-4" />
      <h3 className="text-lg font-semibold text-gray-900 mb-1">
        Import Complete
      </h3>
      <p className="text-sm text-gray-500 mb-4">
        {totalCount} records have been successfully imported.
      </p>
      <button
        onClick={() => onRestart()}
        className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90"
      >
        Start New Import
      </button>
    </div>
  );
}
