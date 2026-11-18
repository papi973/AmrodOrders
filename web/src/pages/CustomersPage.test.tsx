import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CustomersPage } from "./CustomersPage";

const { mutate } = vi.hoisted(() => ({ mutate: vi.fn() }));

vi.mock("../api/hooks", () => ({
  useCustomers: () => ({
    isPending: false,
    isFetching: false,
    error: null,
    data: {
      items: [
        {
          id: "c1",
          name: "Acme Trading",
          email: "buyer@acme.co.za",
          countryCode: "ZA",
          createdAt: "2026-11-17T10:00:00Z",
        },
      ],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
    },
  }),
  useCreateCustomer: () => ({ mutate, isPending: false, isSuccess: false, error: null }),
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <CustomersPage />
    </MemoryRouter>,
  );
}

describe("CustomersPage", () => {
  beforeEach(() => mutate.mockReset());

  it("lists customers with their country name", () => {
    renderPage();

    const row = screen.getByRole("row", { name: /Acme Trading/ });
    expect(row).toHaveTextContent("buyer@acme.co.za");
    expect(row).toHaveTextContent("South Africa");
  });

  it("submits the create form with the entered values", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.type(screen.getByLabelText("Name"), "Gaborone Gifts");
    await user.type(screen.getByLabelText("Email"), "hello@gifts.co.bw");
    await user.selectOptions(screen.getByLabelText("Country"), "BW");
    await user.click(screen.getByRole("button", { name: "Create customer" }));

    expect(mutate).toHaveBeenCalledWith(
      { name: "Gaborone Gifts", email: "hello@gifts.co.bw", countryCode: "BW" },
      expect.anything(),
    );
  });
});
