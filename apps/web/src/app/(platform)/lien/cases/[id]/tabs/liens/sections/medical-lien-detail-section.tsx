import MedicalLienInfo from "@/components/lien/forms/add-medical-lien/medical-lien-info";
import MedicalFacilityProviderInfo from "@/components/lien/forms/add-medical-lien/medical-facility-provider-info";
import MedicalCodesDescription from "@/components/lien/forms/add-medical-lien/medical-codes-description";
import UploadDocuments from "@/components/lien/forms/add-medical-lien/medical-upload-document";
import { CollapsibleSection } from "../../../components/collapsible-section";

export function MedicalLienDetailSection({
  caseId,
  lienId,
  loading,
  data,
  onFormValid,
  onDocumentsUploaded,
  onGoBack,
  onSave,
  invalidForm,
  saving,
}: {
  caseId: string;
  lienId: string;
  loading: boolean;
  data: Record<number, any>;
  onFormValid: (isValid: boolean, data: any, index: number) => void;
  onDocumentsUploaded: () => void;
  onGoBack?: () => void;
  onSave: () => void;
  invalidForm: boolean;
  saving: boolean;
}) {
  return (
    <CollapsibleSection title="Medical Liens" icon="ri-stack-line">
      {!loading && (
        <>
          <div className="border-b-1 pb-6 border-gray-300">
            <MedicalLienInfo
              caseId={caseId}
              lienId={lienId}
              data={data[0]}
              onFormValid={(e: boolean, formData?: any) => {
                onFormValid(e, formData, 0);
              }}
            />
          </div>

          <div className="border-b-1 pb-6 pt-6 border-gray-300">
            <MedicalFacilityProviderInfo
              caseId={caseId}
              lienId={lienId}
              data={data[1]}
              onFormValid={(e: boolean, formData?: any) =>
                onFormValid(e, formData, 1)
              }
            />
          </div>

          <div className="border-b-1 pb-6 pt-6 border-gray-300">
            <MedicalCodesDescription
              caseId={caseId}
              lienId={lienId}
              data={{ ...data[2], ...data[4] }}
              onFormValid={(e: boolean, formData?: any) =>
                onFormValid(e, formData, 2)
              }
              mode="edit"
            />
          </div>

          <div className="border-b-1 pb-6 pt-6 border-gray-300">
            <UploadDocuments
              caseId={caseId}
              lienId={lienId}
              data={data[3]}
              onUploaded={() => onDocumentsUploaded()}
              onFormValid={(e: boolean, formData?: any) =>
                onFormValid(e, formData, 3)
              }
              mode="edit"
            />
          </div>
        </>
      )}

      <div className="flex justify-between mt-6">
        <button
          onClick={onGoBack}
          className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
        >
          Go back
        </button>
        <button
          onClick={onSave}
          disabled={invalidForm || saving}
          className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50"
        >
          {saving ? "Saving..." : "Save"}
        </button>
      </div>
    </CollapsibleSection>
  );
}
