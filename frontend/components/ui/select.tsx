"use client";

import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";
import { Check, ChevronDown, Search } from "lucide-react";

import { cn } from "@/lib/utils";

export type SelectOption = {
  value: string;
  label: string;
  /** Quiet text on the right, such as a time zone's offset or a currency. */
  hint?: string;
};

export interface SelectProps {
  options: readonly SelectOption[];
  value?: string;
  defaultValue?: string;
  /** Receives the picked value, so a form library's field spreads onto it. */
  onChange?: (value: string) => void;
  onBlur?: () => void;
  /** Submits the value with the form through a hidden input. */
  name?: string;
  id?: string;
  disabled?: boolean;
  required?: boolean;
  autoFocus?: boolean;
  /** Shown when the value matches no option. */
  placeholder?: string;
  /** A filter box above the list. On by default past 15 options. */
  searchable?: boolean;
  searchLabel?: string;
  /** Rendered under the control and announced with it. */
  error?: string;
  className?: string;
  "aria-label"?: string;
  "aria-labelledby"?: string;
  "aria-describedby"?: string;
  "aria-invalid"?: boolean | "true" | "false";
}

const TYPEAHEAD_RESET_MS = 500;
const PAGE = 10;

function matches(option: SelectOption, query: string) {
  const needle = query.trim().toLowerCase();
  if (!needle) return true;
  return (
    option.label.toLowerCase().includes(needle) ||
    (option.hint?.toLowerCase().includes(needle) ?? false)
  );
}

/** The text of the field's label, without the control a wrapping label holds. */
function labelText(trigger: HTMLButtonElement | null) {
  const label = trigger?.labels?.[0];
  if (!label) return undefined;
  const copy = label.cloneNode(true) as HTMLElement;
  copy.querySelectorAll("button, input").forEach((node) => node.remove());
  return copy.textContent?.replace(/\s+/g, " ").trim() || undefined;
}

function highlight(label: string, query: string) {
  const needle = query.trim().toLowerCase();
  const at = needle ? label.toLowerCase().indexOf(needle) : -1;
  if (at < 0) return label;
  return (
    <>
      {label.slice(0, at)}
      <strong className="font-semibold">
        {label.slice(at, at + needle.length)}
      </strong>
      {label.slice(at + needle.length)}
    </>
  );
}

/**
 * The app's one dropdown: a button that opens a listbox, following the
 * WAI-ARIA select-only combobox pattern. Focus stays on the button while the
 * list is open and the active option is announced through
 * aria-activedescendant; the searchable variant moves focus into its filter
 * box instead. The list renders in a portal so no overflow container clips
 * it, and flips above the button near the bottom of the screen.
 *
 * Label it like any field: `<label htmlFor>`, a wrapping `<label>`, or
 * `aria-label`.
 */
const Select = React.forwardRef<HTMLButtonElement, SelectProps>(
  (
    {
      options,
      value,
      defaultValue,
      onChange,
      onBlur,
      name,
      id,
      disabled,
      required,
      autoFocus,
      placeholder,
      searchable: searchableProp,
      searchLabel,
      error,
      className,
      "aria-label": ariaLabel,
      "aria-labelledby": ariaLabelledBy,
      "aria-describedby": ariaDescribedBy,
      "aria-invalid": ariaInvalid,
    },
    forwardedRef,
  ) => {
    const baseId = React.useId();
    const listboxId = `${baseId}-listbox`;
    const errorId = `${baseId}-error`;
    const optionId = (index: number) => `${baseId}-option-${index}`;

    const triggerRef = React.useRef<HTMLButtonElement | null>(null);
    const listRef = React.useRef<HTMLDivElement | null>(null);
    const searchRef = React.useRef<HTMLInputElement | null>(null);
    const typed = React.useRef({ text: "", at: 0 });

    const [internal, setInternal] = React.useState(defaultValue ?? "");
    const current = value ?? internal;
    const [open, setOpen] = React.useState(false);
    const [query, setQuery] = React.useState("");
    const [active, setActive] = React.useState(-1);
    // Read from the field's label when it opens, to name the list and filter.
    const [listName, setListName] = React.useState<string | undefined>();

    const searchable = searchableProp ?? options.length > 15;
    const visible = React.useMemo(
      () =>
        searchable && query
          ? options.filter((option) => matches(option, query))
          : options,
      [options, query, searchable],
    );
    const selected = options.find((option) => option.value === current);

    const setRefs = (node: HTMLButtonElement | null) => {
      triggerRef.current = node;
      if (typeof forwardedRef === "function") forwardedRef(node);
      else if (forwardedRef) forwardedRef.current = node;
    };

    const show = (start: "selected" | "first" | "last" | number) => {
      setListName(ariaLabel ?? labelText(triggerRef.current));
      setQuery("");
      const selectedIndex = options.findIndex((o) => o.value === current);
      setActive(
        typeof start === "number"
          ? start
          : start === "first"
            ? 0
            : start === "last"
              ? options.length - 1
              : Math.max(selectedIndex, 0),
      );
      setOpen(true);
    };

    const hide = (returnFocus: boolean) => {
      setOpen(false);
      setQuery("");
      typed.current.text = "";
      if (returnFocus) triggerRef.current?.focus();
    };

    const pick = (option: SelectOption | undefined) => {
      if (!option) return;
      if (value === undefined) setInternal(option.value);
      if (option.value !== current) onChange?.(option.value);
      hide(true);
    };

    /** The first option whose label starts with what was typed. */
    const findTyped = (key: string, from: number) => {
      const now = Date.now();
      const state = typed.current;
      state.text =
        now - state.at > TYPEAHEAD_RESET_MS
          ? key.toLowerCase()
          : state.text + key.toLowerCase();
      state.at = now;
      const repeated = [...state.text].every((c) => c === state.text[0]);
      // Pressing one letter again cycles through the options that start with it.
      const term = repeated ? state.text[0] : state.text;
      const offset = repeated ? 1 : 0;
      for (let step = 0; step < options.length; step++) {
        const index = (from + offset + step + options.length) % options.length;
        if (options[index].label.toLowerCase().startsWith(term)) return index;
      }
      return -1;
    };

    const isTypeKey = (event: React.KeyboardEvent) =>
      event.key.length === 1 &&
      !event.ctrlKey &&
      !event.metaKey &&
      !event.altKey &&
      (event.key !== " " || Date.now() - typed.current.at < TYPEAHEAD_RESET_MS);

    const move = (event: React.KeyboardEvent, count: number) => {
      const last = visible.length - 1;
      let next = active;
      switch (event.key) {
        case "ArrowDown":
          next = Math.min(active + 1, last);
          break;
        case "ArrowUp":
          next = Math.max(active - 1, 0);
          break;
        case "PageDown":
          next = Math.min(active + count, last);
          break;
        case "PageUp":
          next = Math.max(active - count, 0);
          break;
        case "Home":
          next = 0;
          break;
        case "End":
          next = last;
          break;
        default:
          return false;
      }
      event.preventDefault();
      setActive(next);
      return true;
    };

    const onTriggerKeyDown = (
      event: React.KeyboardEvent<HTMLButtonElement>,
    ) => {
      if (disabled) return;
      if (!open) {
        const { key } = event;
        if (
          key === "ArrowDown" ||
          key === "ArrowUp" ||
          key === "Enter" ||
          key === " "
        ) {
          event.preventDefault();
          show("selected");
        } else if (key === "Home" || key === "End") {
          event.preventDefault();
          show(key === "Home" ? "first" : "last");
        } else if (isTypeKey(event)) {
          event.preventDefault();
          if (searchable) {
            show("first");
            setQuery(key);
          } else {
            const found = findTyped(
              key,
              options.findIndex((o) => o.value === current),
            );
            show(found >= 0 ? found : "selected");
          }
        }
        return;
      }
      // Open and not searchable: the button keeps focus and drives the list.
      // Keys it handles stop here, so a dialog around it does not also react.
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        hide(true);
      } else if (event.key === "Tab") {
        hide(false);
      } else if (event.key === "Enter" || event.key === " ") {
        if (event.key === " " && isTypeKey(event)) {
          event.preventDefault();
          const found = findTyped(" ", active);
          if (found >= 0) setActive(found);
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        pick(visible[active]);
      } else if (move(event, PAGE)) {
        event.stopPropagation();
      } else if (isTypeKey(event)) {
        event.preventDefault();
        const found = findTyped(event.key, active);
        if (found >= 0) setActive(found);
      }
    };

    const onSearchKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
      // Home and End stay with the text box, to move its caret.
      if (event.key === "Home" || event.key === "End") return;
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        hide(true);
      } else if (event.key === "Tab") {
        event.preventDefault();
        event.stopPropagation();
        hide(true);
      } else if (event.key === "Enter") {
        event.preventDefault();
        event.stopPropagation();
        pick(visible[active]);
      } else if (move(event, PAGE)) {
        event.stopPropagation();
      }
    };

    // Keep the active option in view without scrolling the page around it.
    React.useLayoutEffect(() => {
      if (!open || active < 0) return;
      const list = listRef.current;
      const item = list?.querySelector<HTMLElement>(
        `[id="${optionId(active)}"]`,
      );
      if (!list || !item) return;
      if (item.offsetTop < list.scrollTop) list.scrollTop = item.offsetTop;
      else if (
        item.offsetTop + item.offsetHeight >
        list.scrollTop + list.clientHeight
      )
        list.scrollTop = item.offsetTop + item.offsetHeight - list.clientHeight;
      // optionId is derived from baseId, which never changes.
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [open, active, visible]);

    const invalid =
      Boolean(error) || ariaInvalid === true || ariaInvalid === "true";
    const describedBy =
      [ariaDescribedBy, error ? errorId : undefined]
        .filter(Boolean)
        .join(" ") || undefined;
    const activeId =
      open && active >= 0 && active < visible.length
        ? optionId(active)
        : undefined;

    return (
      <>
        <PopoverPrimitive.Root
          open={open}
          onOpenChange={(next) => {
            if (!next) hide(false);
          }}
        >
          <PopoverPrimitive.Anchor asChild>
            <button
              ref={setRefs}
              type="button"
              role="combobox"
              id={id}
              aria-haspopup="listbox"
              aria-expanded={open}
              aria-controls={open ? listboxId : undefined}
              aria-activedescendant={searchable ? undefined : activeId}
              aria-label={ariaLabel}
              aria-labelledby={ariaLabelledBy}
              aria-describedby={describedBy}
              aria-invalid={invalid || undefined}
              aria-required={required || undefined}
              disabled={disabled}
              autoFocus={autoFocus}
              data-placeholder={selected ? undefined : ""}
              className={cn(
                "flex min-h-11 w-full items-center gap-2 rounded-[3px] border border-charcoal-blue-300 bg-card pl-3 pr-2.5 text-left text-[15px] text-foreground",
                "transition-colors duration-150 ease-out hover:border-charcoal-blue-400 focus:border-charcoal-blue-900 aria-expanded:border-foreground",
                "dark:border-charcoal-blue-700 dark:hover:border-charcoal-blue-600 dark:focus:border-charcoal-blue-400 dark:aria-expanded:border-foreground",
                "disabled:cursor-not-allowed disabled:opacity-50",
                invalid &&
                  "border-destructive hover:border-destructive dark:border-destructive dark:hover:border-destructive",
                className,
              )}
              onClick={() => (open ? hide(false) : show("selected"))}
              onKeyDown={onTriggerKeyDown}
              onBlur={() => {
                if (!open) onBlur?.();
              }}
            >
              <span
                className={cn(
                  "min-w-0 flex-1 truncate",
                  !selected && "text-muted-foreground",
                )}
              >
                {selected?.label ?? placeholder ?? " "}
              </span>
              {selected?.hint && (
                <span className="shrink-0 text-[13px] text-muted-foreground">
                  {selected.hint}
                </span>
              )}
              <ChevronDown
                aria-hidden="true"
                strokeWidth={1.8}
                className={cn(
                  "size-[18px] shrink-0 text-muted-foreground transition-transform duration-150",
                  open && "rotate-180 text-foreground",
                )}
              />
            </button>
          </PopoverPrimitive.Anchor>
          <PopoverPrimitive.Portal>
            <PopoverPrimitive.Content
              side="bottom"
              align="start"
              sideOffset={4}
              collisionPadding={8}
              onOpenAutoFocus={(event) => {
                event.preventDefault();
                const search = searchRef.current;
                if (!search) return;
                search.focus();
                // A letter typed on the closed button starts the filter.
                search.setSelectionRange(
                  search.value.length,
                  search.value.length,
                );
              }}
              onCloseAutoFocus={(event) => event.preventDefault()}
              // Escape is handled by the focused control, so it can return
              // focus and keep a surrounding dialog open.
              onEscapeKeyDown={(event) => event.preventDefault()}
              onInteractOutside={(event) => {
                if (triggerRef.current?.contains(event.target as Node))
                  event.preventDefault();
              }}
              className="z-[150] flex max-h-[min(20rem,var(--radix-popover-content-available-height))] min-w-(--radix-popover-trigger-width) max-w-[calc(100vw-16px)] flex-col rounded-[3px] border border-border bg-card text-foreground outline-none"
            >
              {searchable && (
                <div className="flex min-h-11 shrink-0 items-center gap-2 border-b border-border px-3 focus-within:border-b-foreground">
                  <Search
                    aria-hidden="true"
                    strokeWidth={1.8}
                    className="size-4 shrink-0 text-muted-foreground"
                  />
                  <input
                    ref={searchRef}
                    type="search"
                    role="combobox"
                    aria-label={
                      searchLabel ??
                      (listName ? `Search ${listName.toLowerCase()}` : "Search")
                    }
                    aria-autocomplete="list"
                    aria-expanded="true"
                    aria-controls={listboxId}
                    aria-activedescendant={activeId}
                    autoComplete="off"
                    spellCheck={false}
                    placeholder="Search"
                    value={query}
                    onChange={(event) => {
                      setQuery(event.target.value);
                      const next = options.filter((o) =>
                        matches(o, event.target.value),
                      );
                      const at = next.findIndex((o) => o.value === current);
                      setActive(next.length === 0 ? -1 : Math.max(at, 0));
                    }}
                    onKeyDown={onSearchKeyDown}
                    className="min-w-0 flex-1 bg-transparent text-base text-foreground outline-none placeholder:text-muted-foreground sm:text-sm [&::-webkit-search-cancel-button]:hidden"
                  />
                </div>
              )}
              <div
                ref={listRef}
                id={listboxId}
                role="listbox"
                aria-label={listName}
                aria-labelledby={listName ? undefined : ariaLabelledBy}
                className="relative min-h-0 flex-1 overflow-y-auto overscroll-contain py-1"
              >
                {visible.map((option, index) => {
                  const isSelected = option.value === current;
                  return (
                    <div
                      key={option.value}
                      id={optionId(index)}
                      role="option"
                      aria-selected={isSelected}
                      className={cn(
                        "flex min-h-10 cursor-pointer items-center gap-2.5 px-3 py-2 text-sm text-foreground",
                        index === active &&
                          "bg-hair shadow-[inset_2px_0_0_var(--foreground)]",
                      )}
                      // Keep focus on the button or the filter box.
                      onMouseDown={(event) => event.preventDefault()}
                      onPointerMove={() => {
                        if (index !== active) setActive(index);
                      }}
                      onClick={() => pick(option)}
                    >
                      <span className="flex size-4 shrink-0 items-center justify-center">
                        {isSelected && (
                          <Check
                            aria-hidden="true"
                            strokeWidth={2.2}
                            className="size-4 text-brand-700 dark:text-brand-400"
                          />
                        )}
                      </span>
                      <span className="min-w-0 flex-1 break-words">
                        {searchable
                          ? highlight(option.label, query)
                          : option.label}
                      </span>
                      {option.hint && (
                        <span className="shrink-0 text-[13px] text-muted-foreground">
                          {option.hint}
                        </span>
                      )}
                    </div>
                  );
                })}
                {visible.length === 0 && (
                  <p className="px-3 py-2.5 text-sm text-muted-foreground">
                    No matches
                  </p>
                )}
              </div>
              {searchable && (
                <div
                  role="status"
                  className="shrink-0 border-t border-border px-3 py-2 text-xs text-muted-foreground"
                >
                  {visible.length} of {options.length}
                </div>
              )}
            </PopoverPrimitive.Content>
          </PopoverPrimitive.Portal>
        </PopoverPrimitive.Root>
        {name && <input type="hidden" name={name} value={current} />}
        {error && (
          <p id={errorId} className="mt-1.5 text-sm text-destructive">
            {error}
          </p>
        )}
      </>
    );
  },
);
Select.displayName = "Select";

export { Select };
