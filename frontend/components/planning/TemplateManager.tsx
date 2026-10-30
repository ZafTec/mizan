"use client";

import { useId, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight } from "lucide-react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import {
  createTemplate,
  deleteTemplate,
  type Template,
} from "@/lib/api/mealPlans";
import {
  emptyRulesForm,
  parseRules,
  parseTemplateForm,
  type RulesFormErrors,
  type RulesFormValues,
  type TemplateFormErrors,
} from "@/lib/planning/rules";
import { appToast, getErrorMessage } from "@/lib/toast";
import RulesFields from "./RulesFields";

/**
 * The templates one keeper manages: an administrator's global ones on
 * /admin/meal-plans, a coach's own on /trainer/templates. Each row opens the
 * plan editor at `${basePath}/${id}`; a new template starts with its rules
 * and opens there to be filled. The API decides who may keep which kind.
 */
export default function TemplateManager({
  templates,
  global,
  basePath,
  empty,
}: {
  templates: Template[];
  global: boolean;
  basePath: string;
  empty: string;
}) {
  const router = useRouter();
  const id = useId();
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [durationDays, setDurationDays] = useState("7");
  const [rules, setRules] = useState<RulesFormValues>(emptyRulesForm);
  const [errors, setErrors] = useState<TemplateFormErrors>({});
  const [rulesErrors, setRulesErrors] = useState<RulesFormErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<Template | null>(null);

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    const parsed = parseTemplateForm({ name, durationDays });
    const parsedRules = parseRules(rules);
    setErrors(parsed.errors ?? {});
    setRulesErrors(parsedRules.errors ?? {});
    if (parsed.errors || parsedRules.errors) return;
    setBusy(true);
    try {
      const created = await createTemplate({
        name: parsed.template.name,
        description: description.trim() || null,
        durationDays: parsed.template.durationDays,
        global,
        rules: parsedRules.rules,
        entries: [],
        prepSessions: [],
      });
      appToast.success(`${created.name ?? "Template"} created`);
      router.push(`${basePath}/${created.id}`);
    } catch (caught) {
      setError(getErrorMessage(caught, "Could not create the template"));
      setBusy(false);
    }
  };

  const remove = async () => {
    const template = deleting;
    if (!template) return;
    await deleteTemplate(template.id);
    appToast.success(`${template.name ?? "Template"} deleted`);
    router.refresh();
  };

  return (
    <div className="space-y-10">
      <section aria-labelledby={`${id}-list`}>
        <header className="log-section-header">
          <h2 id={`${id}-list`}>Templates</h2>
        </header>
        {templates.length === 0 ? (
          <p className="log-empty">{empty}</p>
        ) : (
          <ul>
            {templates.map((template) => (
              <li
                key={template.id}
                className="ledger-row"
                aria-label={template.name ?? "Template"}
              >
                <Link
                  href={`${basePath}/${template.id}`}
                  className="flex min-h-11 min-w-0 flex-1 items-center gap-3"
                >
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium break-words">
                      {template.name}
                    </span>
                    {template.description && (
                      <span className="mt-0.5 block text-sm text-muted-foreground">
                        {template.description}
                      </span>
                    )}
                  </span>
                  <span className="tabular-nums whitespace-nowrap text-sm text-muted-foreground">
                    {template.durationDays} days · {template.entryCount}{" "}
                    {template.entryCount === 1 ? "entry" : "entries"}
                  </span>
                  <ChevronRight
                    aria-hidden="true"
                    size={17}
                    strokeWidth={1.7}
                    className="text-muted-foreground shrink-0"
                  />
                  <span className="sr-only">Edit {template.name}</span>
                </Link>
                <button
                  type="button"
                  className="btn-ghost-destructive btn-sm min-h-11"
                  aria-label={`Delete ${template.name ?? "template"}`}
                  onClick={() => setDeleting(template)}
                >
                  Delete
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby={`${id}-new`}>
        <header className="log-section-header">
          <h2 id={`${id}-new`}>New template</h2>
        </header>
        {!creating ? (
          <button
            type="button"
            className="btn-primary min-h-11 w-full sm:w-auto"
            onClick={() => setCreating(true)}
          >
            New template
          </button>
        ) : (
          <form
            onSubmit={create}
            className="space-y-8"
            noValidate
            aria-label="New template"
          >
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label htmlFor={`${id}-name`} className="label">
                  Name
                </label>
                <input
                  id={`${id}-name`}
                  type="text"
                  className="input w-full"
                  value={name}
                  autoFocus
                  onChange={(event) => setName(event.target.value)}
                  aria-invalid={errors.name ? true : undefined}
                />
                {errors.name && (
                  <p className="input-error mt-1 text-sm">{errors.name}</p>
                )}
              </div>
              <div>
                <label htmlFor={`${id}-days`} className="label">
                  Length (days)
                </label>
                <input
                  id={`${id}-days`}
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={31}
                  className="input tabular-nums w-full"
                  value={durationDays}
                  onChange={(event) => setDurationDays(event.target.value)}
                  aria-invalid={errors.durationDays ? true : undefined}
                />
                {errors.durationDays && (
                  <p className="input-error mt-1 text-sm">
                    {errors.durationDays}
                  </p>
                )}
              </div>
              <div className="sm:col-span-2">
                <label htmlFor={`${id}-description`} className="label">
                  Description
                </label>
                <input
                  id={`${id}-description`}
                  type="text"
                  className="input w-full"
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                />
              </div>
            </div>
            <RulesFields
              values={rules}
              errors={rulesErrors}
              durationDays={Number(durationDays) || 1}
              onChange={setRules}
            />
            {error && (
              <p role="alert" className="input-error text-sm">
                {error}
              </p>
            )}
            <div className="flex flex-wrap gap-2">
              <button
                type="submit"
                className="btn-primary min-h-11 w-full sm:w-auto"
                disabled={busy}
              >
                Create template
              </button>
              <button
                type="button"
                className="btn-ghost min-h-11"
                onClick={() => setCreating(false)}
              >
                Cancel
              </button>
            </div>
          </form>
        )}
      </section>

      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        title={`Delete ${deleting?.name ?? "this template"}?`}
        description="The template is removed. Plans already made from it keep their copy."
        pendingLabel="Deleting…"
        errorFallback="Could not delete the template"
        onConfirm={remove}
      />
    </div>
  );
}
