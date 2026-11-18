import { useRef } from "react";
import { Link, useParams } from "react-router";
import type { OrderStatus } from "../api/client";
import { useOrder, useUpdateOrderStatus } from "../api/hooks";
import { ProblemAlert } from "../components/ProblemAlert";
import { formatMoney } from "../sadc";

// Mirrors the domain state machine (user-initiated transitions only).
const NEXT: Record<string, OrderStatus[]> = {
  Pending: ["Paid", "Cancelled"],
  Paid: ["Fulfilled", "Cancelled"],
  Fulfilled: [],
  Cancelled: [],
};

export function OrderDetailsPage() {
  const { id = "" } = useParams();
  const order = useOrder(id);
  const update = useUpdateOrderStatus(id);

  // One Idempotency-Key per intended transition: reused on retry, new for a different action.
  const pending = useRef<{ status: OrderStatus; key: string } | null>(null);

  function transition(status: OrderStatus) {
    if (!order.data) return;
    if (pending.current?.status !== status) {
      pending.current = { status, key: crypto.randomUUID() };
    }
    update.mutate(
      { status, idempotencyKey: pending.current.key, etag: `"${order.data.version}"` },
      {
        onSuccess: () => {
          pending.current = null;
        },
      },
    );
  }

  if (order.isPending) return <p aria-live="polite">Loading order…</p>;
  if (order.error) return <ProblemAlert error={order.error} />;

  const o = order.data;
  const actions = NEXT[o.status] ?? [];

  return (
    <section>
      <p>
        <Link to="/orders">← Back to orders</Link>
      </p>
      <h1>Order {o.id.slice(0, 8)}</h1>

      <dl className="meta">
        <dt>Status</dt>
        <dd>
          <span className={`badge badge-${o.status.toLowerCase()}`}>{o.status}</span>
        </dd>
        <dt>Created</dt>
        <dd>{new Date(o.createdAt).toLocaleString()}</dd>
        <dt>Currency</dt>
        <dd>{o.currencyCode}</dd>
        <dt>Customer</dt>
        <dd>
          <Link to={`/orders?customerId=${o.customerId}`}>{o.customerId}</Link>
        </dd>
      </dl>

      {o.status === "Pending" && (
        <p aria-live="polite" className="hint">
          Waiting for allocation… the worker will fulfil this order shortly.
        </p>
      )}

      <table>
        <thead>
          <tr>
            <th scope="col">SKU</th>
            <th scope="col" className="num">
              Qty
            </th>
            <th scope="col" className="num">
              Unit price
            </th>
            <th scope="col" className="num">
              Line total
            </th>
          </tr>
        </thead>
        <tbody>
          {o.lineItems.map((li) => (
            <tr key={li.id}>
              <td>{li.productSku}</td>
              <td className="num">{li.quantity}</td>
              <td className="num">{formatMoney(li.unitPrice, o.currencyCode)}</td>
              <td className="num">{formatMoney(li.lineTotal, o.currencyCode)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th scope="row" colSpan={3}>
              Total
            </th>
            <td className="num">
              <strong>{formatMoney(o.totalAmount, o.currencyCode)}</strong>
            </td>
          </tr>
        </tfoot>
      </table>

      {actions.length > 0 && (
        <div className="actions" role="group" aria-label="Change status">
          {actions.map((s) => (
            <button key={s} type="button" disabled={update.isPending} onClick={() => transition(s)}>
              {update.isPending && update.variables?.status === s ? "Updating…" : `Mark as ${s}`}
            </button>
          ))}
        </div>
      )}
      <ProblemAlert error={update.error} />
    </section>
  );
}
