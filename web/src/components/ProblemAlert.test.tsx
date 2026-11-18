import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ApiError } from "../api/client";
import { ProblemAlert } from "./ProblemAlert";

describe("ProblemAlert", () => {
  it("renders nothing when there is no error", () => {
    const { container } = render(<ProblemAlert error={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows the title, validation messages and correlation id", () => {
    const error = new ApiError(400, {
      title: "One or more validation errors occurred.",
      errors: { CountryCode: ["'KE' is not a SADC country code (ISO 3166-1 alpha-2)."] },
      correlationId: "abc123",
    });

    render(<ProblemAlert error={error} />);

    expect(screen.getByRole("alert")).toHaveTextContent("One or more validation errors occurred.");
    expect(screen.getByText(/not a SADC country code/)).toBeInTheDocument();
    expect(screen.getByText(/abc123/)).toBeInTheDocument();
  });
});
