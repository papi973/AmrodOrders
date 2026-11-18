import { Navigate, NavLink, Route, Routes } from "react-router";
import { CustomersPage } from "./pages/CustomersPage";
import { OrderDetailsPage } from "./pages/OrderDetailsPage";
import { OrdersPage } from "./pages/OrdersPage";

export default function App() {
  return (
    <>
      <header className="topbar">
        <strong>Amrod Orders</strong>
        <nav aria-label="Main">
          <NavLink to="/customers">Customers</NavLink>
          <NavLink to="/orders">Orders</NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/orders" replace />} />
          <Route path="/customers" element={<CustomersPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="/orders/:id" element={<OrderDetailsPage />} />
          <Route path="*" element={<p>Page not found.</p>} />
        </Routes>
      </main>
    </>
  );
}
