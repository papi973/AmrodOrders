import { ApiError } from "../api/client";

export function ProblemAlert({ error }: { error: unknown }) {
  if (!error) return null;

  const problem = error instanceof ApiError ? error.problem : undefined;
  const title = problem?.title ?? "Something went wrong";
  const message = error instanceof Error ? error.message : undefined;
  const fieldErrors = problem?.errors ? Object.values(problem.errors).flat() : [];

  return (
    <div role="alert" className="alert">
      <strong>{title}</strong>
      {message && message !== title && <p>{message}</p>}
      {fieldErrors.length > 0 && (
        <ul>
          {fieldErrors.map((m) => (
            <li key={m}>{m}</li>
          ))}
        </ul>
      )}
      {problem?.correlationId && <small>Reference: {problem.correlationId}</small>}
    </div>
  );
}
