import { useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import type { Customer, OrderStatus } from "../api/client";
import { useCreateOrder, useCustomers, useOrders } from "../api/hooks";
import { Pagination } from "../components/Pagination";
import { ProblemAlert } from "../components/ProblemAlert";
import { currenciesFor, formatMoney } from "../sadc";

const STATUSES: OrderStatus[] = ["Pending", "Paid", "Fulfilled", "Cancelled"];
const SORTS = [
  { value: "-createdAt", label: "Newest first" },
  { value: "createdAt", label: "Oldest first" },
  { value: "-total", label: "Total: high to low" },
  { value: "total", label: "Total: low to high" },
];

export function OrdersPage() {
  // Filters live in the URL: shareable, survive refresh, back button works.
  const [params, setParams] = useSearchParams();
  const query = {
    customerId: params.get("customerId") ?? "",
    status: (params.get("status") ?? "") as OrderStatus | "",
    sort: params.get("sort") ?? "-createdAt",
    page: Number(params.get("page") ?? 1),
    pageSize: 10,
  };

  const setFilter = (changes: Record<string, string>) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(changes)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    if (!("page" in changes)) next.set("page", "1");
    setParams(next);
  };

  const customers = useCustomers({ page: 1, pageSize: 100 });
  const orders = useOrders({ ...query, status: query.status || undefined });
  const customerName = (id: string) =>
    customers.data?.items.find((c) => c.id === id)?.name ?? id.slice(0, 8);

  return (
    <section>
      <h1>Orders</h1>
      {customers.data && <CreateOrderForm customers={customers.data.items} />}

      <div className="toolbar">
        <label>
          Customer
          <select value={query.customerId} onChange={(e) => setFilter({ customerId: e.target.value })}>
            <option value="">All</option>
            {customers.data?.items.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Status
          <select value={query.status} onChange={(e) => setFilter({ status: e.target.value })}>
            <option value="">All</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
        <label>
          Sort
          <select value={query.sort} onChange={(e) => setFilter({ sort: e.target.value })}>
            {SORTS.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      {orders.isPending && <p aria-live="polite">Loading orders…</p>}
      <ProblemAlert error={orders.error} />

      {orders.data && (
        <>
          {orders.data.items.length === 0 ? (
            <p>No orders match these filters.</p>
          ) : (
            <table aria-busy={orders.isFetching}>
              <thead>
                <tr>
                  <th scope="col">Created</th>
                  <th scope="col">Customer</th>
                  <th scope="col">Status</th>
                  <th scope="col" className="num">
                    Total
                  </th>
                  <th scope="col">
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {orders.data.items.map((o) => (
                  <tr key={o.id}>
                    <td>{new Date(o.createdAt).toLocaleString()}</td>
                    <td>{customerName(o.customerId)}</td>
                    <td>
                      <span className={`badge badge-${o.status.toLowerCase()}`}>{o.status}</span>
                    </td>
                    <td className="num">{formatMoney(o.totalAmount, o.currencyCode)}</td>
                    <td>
                      <Link to={`/orders/${o.id}`}>Details</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <Pagination
            page={query.page}
            totalPages={orders.data.totalPages ?? 1}
            onPageChange={(page) => setFilter({ page: String(page) })}
          />
        </>
      )}
    </section>
  );
}

interface LineDraft {
  productSku: string;
  quantity: number;
  unitPrice: number;
}

const emptyLine = (): LineDraft => ({ productSku: "", quantity: 1, unitPrice: 0 });

function CreateOrderForm({ customers }: { customers: Customer[] }) {
  const navigate = useNavigate();
  const create = useCreateOrder();
  const [customerId, setCustomerId] = useState("");
  const [currencyCode, setCurrencyCode] = useState("");
  const [lines, setLines] = useState<LineDraft[]>([emptyLine()]);

  const customer = customers.find((c) => c.id === customerId);
  const currencies = customer ? currenciesFor(customer.countryCode) : [];
  const estimate = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);

  const chooseCustomer = (id: string) => {
    setCustomerId(id);
    const c = customers.find((x) => x.id === id);
    setCurrencyCode(c ? (currenciesFor(c.countryCode)[0] ?? "") : "");
  };

  const updateLine = (i: number, patch: Partial<LineDraft>) =>
    setLines(lines.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));

  function submit(e: FormEvent) {
    e.preventDefault();
    create.mutate(
      { customerId, currencyCode, lineItems: lines },
      { onSuccess: (order) => navigate(`/orders/${order.id}`) },
    );
  }

  return (
    <form className="card" onSubmit={submit} aria-labelledby="new-order-heading">
      <h2 id="new-order-heading">New order</h2>
      <div className="grid">
        <label>
          Customer
          <select required value={customerId} onChange={(e) => chooseCustomer(e.target.value)}>
            <option value="" disabled>
              Select a customer…
            </option>
            {customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.countryCode})
              </option>
            ))}
          </select>
        </label>
        <label>
          Currency
          <select
            required
            value={currencyCode}
            disabled={!customer}
            onChange={(e) => setCurrencyCode(e.target.value)}
          >
            {currencies.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </label>
      </div>

      <fieldset>
        <legend>Line items</legend>
        {lines.map((line, i) => (
          <div className="line" key={i}>
            <label>
              SKU
              <input
                required
                maxLength={64}
                value={line.productSku}
                onChange={(e) => updateLine(i, { productSku: e.target.value })}
              />
            </label>
            <label>
              Qty
              <input
                required
                type="number"
                min={1}
                step={1}
                value={line.quantity}
                onChange={(e) => updateLine(i, { quantity: e.target.valueAsNumber || 0 })}
              />
            </label>
            <label>
              Unit price
              <input
                required
                type="number"
                min={0}
                step="0.01"
                value={line.unitPrice}
                onChange={(e) => updateLine(i, { unitPrice: e.target.valueAsNumber || 0 })}
              />
            </label>
            <button
              type="button"
              aria-label={`Remove line ${i + 1}`}
              disabled={lines.length === 1}
              onClick={() => setLines(lines.filter((_, idx) => idx !== i))}
            >
              Remove
            </button>
          </div>
        ))}
        <button type="button" onClick={() => setLines([...lines, emptyLine()])}>
          Add line
        </button>
      </fieldset>

      {currencyCode && (
        <p>
          Estimated total: <strong>{formatMoney(estimate, currencyCode)}</strong>{" "}
          <small>(final total calculated by the server)</small>
        </p>
      )}
      <button type="submit" disabled={create.isPending || !customer}>
        {create.isPending ? "Creating…" : "Create order"}
      </button>
      <ProblemAlert error={create.error} />
    </form>
  );
}
