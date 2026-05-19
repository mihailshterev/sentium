interface ModelSelectorProps {
  id?: string;
  className: string;
  models: string[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
}

const ModelSelector = ({ id, className, models, value, onChange, placeholder, disabled }: ModelSelectorProps) => {
  if (models.length > 0) {
    return (
      <select
        id={id}
        className={className}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
      >
        {models.map((m) => (
          <option key={m} value={m}>
            {m}
          </option>
        ))}
      </select>
    );
  }

  return (
    <input
      id={id}
      className={className}
      type="text"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder ?? "e.g. gemma3:1b"}
      autoComplete="off"
      spellCheck={false}
      disabled={disabled}
    />
  );
};

export default ModelSelector;
