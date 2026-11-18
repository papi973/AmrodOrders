import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { useCreateCustomer, useCustomers } from "../api/hooks";
import { Pagination } from "../components/Pagination";
import { ProblemAlert } from "../components/ProblemAlert";
import { SADC } from "../sadc";

const PAGE_SIZE = 10;

export function CustomersPage() {
  const [searchInput, setSearchInput] = useState("");
  const [query, setQuery] = useState({ search: "", page: 1, pageSize: PAGE_SIZE });
  const customers = useCustomers(query);

  return (
    <section>
      <h1>Customers</h1>
      <CreateCustomerForm />

      <form
        role="search"
        className="toolbar"
        onSubmit={(e) => {
          e.preventDefault();
          setQuery({ ...query, search: searchInput, page: 1 });
        }}
      >
        <label htmlFor="customer-search">Search</label>
        <input
          id="customer-search"
          type="search"
          placeholder="Name or email"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
        <button type="submit">Search</button>
      </form>

      {customers.isPending && <p aria-live="polite">Loading customers…</p>}
      <ProblemAlert error={customers.error} />

      {customers.data && (
        <>
          {customers.data.items.length === 0 ? (
            <p>No customers found.</p>
          ) : (
            <table aria-busy={customers.isFetching}>
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Email</th>
                  <th scope="col">Country</th>
                  <th scope="col">Created</th>
                  <th scope="col">
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {customers.data.items.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td>
                    <td>{c.email}</td>
                    <td>{SADC[c.countryCode]?.name ?? c.countryCode}</td>
                    <td>{new Date(c.createdAt).toLocaleDateString()}</td>
                    <td>
                      <Link to={`/orders?customerId=${c.id}`}>View orders</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <Pagination
            page={query.page}
            totalPages={customers.data.totalPages ?? 1}
            onPageChange={(page) => setQuery({ ...query, page })}
          />
        </>
      )}
    </section>
  );
}

function CreateCustomerForm() {
  const empty = { name: "", email: "", countryCode: "ZA" };
  const [form, setForm] = useState(empty);
  const create = useCreateCustomer();

  function submit(e: FormEvent) {
    e.preventDefault();
    create.mutate(form, { onSuccess: () => setForm(empty) });
  }

  return (
    <form className="card" onSubmit={submit} aria-labelledby="new-customer-heading">
      <h2 id="new-customer-heading">New customer</h2>
      <div className="grid">
        <label>
          Name
          <input
            required
            maxLength={200}
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
          />
        </label>
        <label>
          Email
          <input
            required
            type="email"
            maxLength={320}
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
          />
        </label>
        <label>
          Country
          <select
            value={form.countryCode}
            onChange={(e) => setForm({ ...form, countryCode: e.target.value })}
          >
            {Object.entries(SADC).map(([code, c]) => (
              <option key={code} value={code}>
                {c.name} ({code})
              </option>
            ))}
          </select>
        </label>
      </div>
      <button type="submit" disabled={create.isPending}>
        {create.isPending ? "Saving…" : "Create customer"}
      </button>
      {create.isSuccess && <p role="status">Customer created.</p>}
      <ProblemAlert error={create.error} />
    </form>
  );
}
