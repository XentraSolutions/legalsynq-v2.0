import Field from "@/components/lien/field";
import type { DropdownOption } from "@/lib/lookup/lookup.types";
import { CollapsibleSection } from "../../../../components/collapsible-section";

export function ServicingDetailsSection({
  caseStatus,
  onCaseStatusChange,
  caseStatusList,
  switchedLawFirm,
  onSwitchedLawFirmChange,
  switchedDate,
  onSwitchedDateChange,
  currentLawFirm,
  onCurrentLawFirmChange,
  lawFirmList,
  onLoadLawFirms,
  currentLawyer,
  onCurrentLawyerChange,
  lawyerList,
  onLoadLawyers,
  currentCaseManager,
  onCurrentCaseManagerChange,
  caseManagerList,
  onLoadCaseManagers,
  onSave,
}: {
  caseStatus: string;
  onCaseStatusChange: (v: string) => void;
  caseStatusList: DropdownOption[];
  switchedLawFirm: boolean;
  onSwitchedLawFirmChange: (v: boolean) => void;
  switchedDate: string;
  onSwitchedDateChange: (v: string) => void;
  currentLawFirm: string;
  onCurrentLawFirmChange: (v: string) => void;
  lawFirmList: DropdownOption[];
  onLoadLawFirms: () => void;
  currentLawyer: string;
  onCurrentLawyerChange: (v: string) => void;
  lawyerList: DropdownOption[];
  onLoadLawyers: () => void;
  currentCaseManager: string;
  onCurrentCaseManagerChange: (v: string) => void;
  caseManagerList: DropdownOption[];
  onLoadCaseManagers: () => void;
  onSave: () => void;
}) {
  return (
    <CollapsibleSection title="Servicing Details" icon="ri-settings-3-line">
      <div className="space-y-4">
        <div>
          <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
            Case Status
          </label>
          <Field
            label=""
            value={caseStatus}
            type="select"
            options={caseStatusList}
            onChange={(e: string) => onCaseStatusChange(e.toString())}
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={switchedLawFirm}
                onChange={(e) => onSwitchedLawFirmChange(e.target.checked)}
                className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary/30 cursor-pointer"
              />
              <span className="text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                Switched Law Firm
              </span>
            </label>
          </div>
          <div>
            <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
              Switched Date
            </label>
            <Field
              label=""
              type="date"
              value={switchedDate}
              onChange={onSwitchedDateChange}
              disabled={!switchedLawFirm}
            />
          </div>
        </div>

        <div>
          <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
            Current Law Firm
          </label>
          <Field
            label=""
            disabled={!switchedLawFirm}
            value={currentLawFirm}
            type="select"
            options={lawFirmList}
            onChange={(e: string) => onCurrentLawFirmChange(e.toString())}
            onClick={onLoadLawFirms}
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
              Current Lawyer
            </label>
            <Field
              label=""
              disabled={!switchedLawFirm}
              value={currentLawyer}
              type="select"
              options={lawyerList}
              onChange={(e: string) => onCurrentLawyerChange(e.toString())}
              onClick={onLoadLawyers}
            />
          </div>
          <div>
            <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
              Current Case Manager
            </label>
            <Field
              label=""
              disabled={!switchedLawFirm}
              value={currentCaseManager}
              type="select"
              options={caseManagerList}
              onChange={(e: string) => onCurrentCaseManagerChange(e.toString())}
              onClick={onLoadCaseManagers}
            />
          </div>
        </div>

        <div className="pt-2 flex items-center gap-3">
          <button
            disabled={!switchedLawFirm}
            onClick={onSave}
            className="px-6 py-2.5 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <i className="ri-save-line text-sm" />
            Save
          </button>
        </div>
      </div>
    </CollapsibleSection>
  );
}
