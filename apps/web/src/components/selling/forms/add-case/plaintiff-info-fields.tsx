import Field from "@/components/lien/field";
import { useSessionContext } from "@/providers/session-provider";
import { formatPhoneInput, isValidPhone } from "@/lib/phone";

export interface PlaintiffInfoFieldsValue {
  firstName: string;
  lastName: string;
  birthdate: string;
  email: string;
  phone: string;
  sex: string;
  address: string;
  city: string;
  state: string;
  zipcode: string;
}

export const PLAINTIFF_INFO_INITIAL_FORM: PlaintiffInfoFieldsValue = {
  firstName: "",
  lastName: "",
  birthdate: "",
  email: "",
  phone: "",
  sex: "",
  address: "",
  city: "",
  state: "",
  zipcode: "",
};

const SEX_OPTIONS = [
  { key: "Male", value: "Male", label: "Male" },
  { key: "Female", value: "Female", label: "Female" },
  { key: "Other", value: "Other", label: "Other" },
];

export function PlaintiffInfoFields({
  value,
  onChange,
}: {
  value: PlaintiffInfoFieldsValue;
  onChange: (patch: Partial<PlaintiffInfoFieldsValue>) => void;
}) {
  const { lookup } = useSessionContext();
  const stateList =
    lookup?.State.map((c) => ({ key: c.id, value: c.id, label: c.name })) ?? [];

  return (
    <div className="grid grid-cols-2 gap-4">
      <Field
        required
        label="First Name"
        value={value.firstName}
        placeholder="Enter first name"
        onChange={(v) => onChange({ firstName: v.toString() })}
      />
      <Field
        required
        label="Last Name"
        value={value.lastName}
        placeholder="Enter last name"
        onChange={(v) => onChange({ lastName: v.toString() })}
      />
      <Field
        required
        label="Birthdate"
        type="date"
        value={value.birthdate}
        maxDate={new Date()}
        onChange={(v) => onChange({ birthdate: v.toString() })}
      />
      <Field
        label="Email"
        type="email"
        value={value.email}
        placeholder="e.g. example@gmail.com"
        onChange={(v) => onChange({ email: v.toString() })}
      />
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Phone Number
        </label>
        <input
          type="text"
          value={value.phone}
          onChange={(e) => onChange({ phone: formatPhoneInput(e.target.value) })}
          placeholder="(555) 555-0000"
          className={`w-full border rounded-lg px-3 py-2 text-sm ${
            value.phone && !isValidPhone(value.phone)
              ? "border-red-300"
              : "border-gray-200"
          } focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`}
        />
        {value.phone && !isValidPhone(value.phone) && (
          <p className="text-xs text-red-500 mt-1">
            Phone number must be 10 digits
          </p>
        )}
      </div>
      <Field
        label="Sex"
        type="select"
        value={value.sex}
        options={SEX_OPTIONS}
        placeholder="Select sex"
        onChange={(v: string) => onChange({ sex: v.toString() })}
      />
      <Field
        label="Address"
        value={value.address}
        placeholder="Enter address"
        onChange={(v) => onChange({ address: v.toString() })}
      />
      <Field
        label="City"
        value={value.city}
        placeholder="Enter city"
        onChange={(v) => onChange({ city: v.toString() })}
      />
      <Field
        label="State"
        type="select"
        value={value.state}
        options={stateList}
        placeholder="Select state"
        onChange={(v: string) => onChange({ state: v.toString() })}
      />
      <Field
        label="Zip Code"
        value={value.zipcode}
        placeholder="Enter zip code"
        onChange={(v) => onChange({ zipcode: v.toString() })}
      />
    </div>
  );
}
