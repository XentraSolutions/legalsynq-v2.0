import { useEffect, useRef, useState, type ReactNode } from "react";
import {
  BaseSelect,
  BaseSelectOption,
  BaseSelectProps,
} from "../ui/base-select";
import { DatePicker } from "../ui/date-picker";
import { PhoneInput } from "../ui/phone-input";

interface BaseFieldProps {
  label: string;
  onFocus?: () => void;
  onClick?: () => void;
  error?: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  prefix?: ReactNode;
  suffix?: ReactNode;
  children?: ReactNode;
}

interface TextFieldProps {
  type?: "text" | "textarea" | "number" | "email";
  value?: string | null | number;
  onChange: (value: string) => void;
}

interface DateFieldProps {
  type?: "date";
  maxDate?: Date | null;
  value?: string;
  onChange: (value: string) => void;
}
export interface PhoneFieldProps {
  type: "tel";
  value: string; // Formatted input value managed by parent state
  onChange: (value: string) => void; // Emits the formatted string upward
}
interface CheckboxFieldProps {
  type: "checkbox";
  isChecked?: boolean;
  onChange: (value: boolean) => void;
}

interface SelectFieldProps<TOption extends BaseSelectOption> {
  type: "select";
  options: TOption[];

  multiple?: false;
  value?: string | null;
  onChange: (value: string, option: TOption) => void;
}

interface MultiSelectFieldProps<TOption extends BaseSelectOption> {
  type: "select";
  options: TOption[];

  multiple: true;
  value?: string[];
  onChange: (values: string[], options: TOption[]) => void;
}

export type FieldProps<TOption extends BaseSelectOption = BaseSelectOption> =
  BaseFieldProps &
    (
      | TextFieldProps
      | CheckboxFieldProps
      | DateFieldProps
      | PhoneFieldProps
      | SelectFieldProps<TOption>
      | MultiSelectFieldProps<TOption>
    );

export default function Field<
  TOption extends BaseSelectOption = BaseSelectOption,
>(props: FieldProps<TOption>) {
  const {
    label,
    onFocus,
    onClick,
    error,
    placeholder,
    required,
    children,
    disabled,
    prefix,
    suffix,
  } = props;

  const hasAdornment = Boolean(prefix || suffix);
  const inputPadding = `${prefix ? "pl-9" : "pl-3"} ${suffix ? "pr-9" : "pr-3"}`;

  return (
    <div
      className={
        props.type === "checkbox"
          ? "flex items-center gap-3 cursor-pointer"
          : "overflow-visible"
      }
    >
      <label className="block text-sm font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
      </label>

      {props.type === "textarea" ? (
        <textarea
          value={props.value ?? ""}
          onChange={(e) => props.onChange(e.target.value)}
          onFocus={onFocus}
          onClick={onClick}
          placeholder={placeholder}
          className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${
            error ? "border-red-300" : "border-gray-200"
          }`}
          disabled={disabled}
        >
          {children}
        </textarea>
      ) : props.type === "select" && props.multiple === true ? (
        <BaseSelect
          multiple
          value={props.value}
          onChange={props.onChange}
          options={props.options}
          placeholder={props.placeholder}
          disabled={props.disabled}
          error={!!props.error}
        />
      ) : props.type === "select" ? (
        <BaseSelect
          value={props.value}
          onChange={props.onChange}
          options={props.options}
          placeholder={props.placeholder}
          disabled={props.disabled}
          error={!!props.error}
        />
      ) : props.type === "checkbox" ? (
        <input
          type="checkbox"
          checked={props.isChecked}
          onChange={(e) => props.onChange(e.target.checked)}
          className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary mb-1"
          disabled={disabled}
        />
      ) : props.type === "date" ? (
        <DatePicker
          maxDate={props?.maxDate}
          value={props.value}
          onChange={props.onChange}
        />
      ) : props.type === "tel" ? (
        <PhoneInput
          label=""
          value={props.value}
          onValueChange={props.onChange}
          className="shadow-sm" // Optional styling overrides
        />
      ) : (
        <div className={hasAdornment ? "relative" : ""}>
          {prefix && (
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400">
              {prefix}
            </span>
          )}

          <input
            type={props.type}
            value={props.value ?? ""}
            onChange={(e) => props.onChange(e.target.value)}
            onFocus={onFocus}
            onClick={onClick}
            placeholder={placeholder}
            disabled={disabled}
            className={`w-full border rounded-lg ${inputPadding} py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${
              error ? "border-red-300" : "border-gray-200"
            }`}
          />

          {suffix && (
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-gray-400">
              {suffix}
            </span>
          )}
        </div>
      )}

      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}
