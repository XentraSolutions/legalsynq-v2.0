export type CreateCaseFormState = {
  caseNumber: string;
  clientFirstName: string;
  clientLastName: string;
  externalReference: string;
  title: string;
  clientDob: string;
  clientPhone: string;
  clientEmail: string;
  clientAddress: string;
  dateOfIncident: string;
  insuranceCarrier: string;
  policyNumber: string;
  claimNumber: string;
  description: string;
  notes: string;
  clientCity?: string;
  clientState?: string;
  clientZipcode?: string;
  dateOfLoss?: string;
  caseStatusId: string | null;
  caseManagerId?: string | null;
  lawfirmId?: string | null;
  stateId?: string | null;
  accidentTypeId?: string | null;
  accidentStateId?: string | null;
};

type FieldType = "text" | "email" | "date" | "tel";

type FieldValidationConfig = {
  field: keyof CreateCaseFormState;
  type?: FieldType;
  required?: boolean;
  requiredMessage: string;
  invalidMessage?: string;
};

const EMAIL_REGEX = /\S+@\S+\.\S+/;
const PHONE_REGEX = /^[0-9()\-+\s]{7,20}$/;

const fieldValidationConfigs: FieldValidationConfig[] = [
  {
    field: "clientFirstName",
    required: true,
    requiredMessage: "First name is required",
  },
  {
    field: "clientLastName",
    required: true,
    requiredMessage: "Last name is required",
  },
  {
    field: "clientDob",
    type: "date",
    required: true,
    requiredMessage: "Date of Birth is required",
    invalidMessage: "Please enter a valid date of birth",
  },
  {
    field: "clientPhone",
    type: "tel",
    required: false,
    requiredMessage: "",
    invalidMessage: "Please enter a valid phone number",
  },
  {
    field: "clientEmail",
    type: "email",
    required: false,
    requiredMessage: "Email is required",
    invalidMessage: "Please enter a valid email address",
  },
  {
    field: "caseStatusId",
    required: true,
    requiredMessage: "Status is required",
    invalidMessage: "Please select a status",
  },

  {
    field: "accidentTypeId",
    required: true,
    requiredMessage: "Accident Type is required",
    invalidMessage: "Please select an Accident Type",
  },
  {
    field: "lawfirmId",
    required: true,
    requiredMessage: "Lawfirm is required",
    invalidMessage: "Please select an Lawfirm",
  },
];

const getFieldError = (
  fieldConfig: FieldValidationConfig,
  form: CreateCaseFormState,
): string | undefined => {
  const rawValue = form[fieldConfig.field];
  const value = rawValue?.toString().trim() ?? "";

  if (fieldConfig.required && !value) {
    return fieldConfig.requiredMessage;
  }

  if (!value) {
    return undefined;
  }

  switch (fieldConfig.type) {
    case "email":
      return EMAIL_REGEX.test(value) ? undefined : fieldConfig.invalidMessage;
    case "tel":
      return PHONE_REGEX.test(value) ? undefined : fieldConfig.invalidMessage;
    case "date": {
      const parsed = new Date(value);
      return Number.isNaN(parsed.getTime())
        ? fieldConfig.invalidMessage
        : undefined;
    }
    default:
      return undefined;
  }
};

export function getCreateCaseFormErrors(
  form: CreateCaseFormState,
): Partial<Record<keyof CreateCaseFormState, string>> {
  const errors: Partial<Record<keyof CreateCaseFormState, string>> = {};

  fieldValidationConfigs.forEach((config) => {
    const error = getFieldError(config, form);
    if (error) {
      errors[config.field] = error;
    }
  });

  return errors;
}

export function isCreateCaseFormValid(form: CreateCaseFormState): boolean {
  return Object.keys(getCreateCaseFormErrors(form)).length === 0;
}
