import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, unwrap, type CreateCustomerBody, type CreateOrderBody, type OrderStatus } from "./client";

// Customers 
export interface CustomerQuery { search?: string; page: number; pageSize: number }

export function useCustomers(q: CustomerQuery) {
    return useQuery({
        queryKey: ["customers", q],
        queryFn: () =>
            unwrap(api.GET("/api/customers", {
                params: { query: { search: q.search || undefined, page: q.page, pageSize: q.pageSize } },
            })),
        placeholderData: keepPreviousData, // keep the old page visible while the next one loads
    });
}

export function useCreateCustomer() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: CreateCustomerBody) => unwrap(api.POST("/api/customers", { body })),
        onSuccess: () => qc.invalidateQueries({ queryKey: ["customers"] }),
    });
}

//  Orders
export interface OrderQuery { customerId?: string; status?: OrderStatus; sort: string; page: number; pageSize: number }

export function useOrders(q: OrderQuery) {
    return useQuery({
        queryKey: ["orders", q],
        queryFn: () =>
            unwrap(api.GET("/api/orders", {
                params: {
                    query: {
                        customerId: q.customerId || undefined,
                        status: q.status || undefined,
                        sort: q.sort,
                        page: q.page,
                        pageSize: q.pageSize,
                    },
                },
            })),
        placeholderData: keepPreviousData,
    });
}

export function useOrder(id: string) {
    return useQuery({
        queryKey: ["order", id],
        queryFn: () => unwrap(api.GET("/api/orders/{id}", { params: { path: { id } } })),
        enabled: !!id,
        // While Pending, poll so the user sees the worker fulfil it. (The browser revalidates with the ETag → cheap 304s.)
        refetchInterval: (query) => (query.state.data?.status === "Pending" ? 2000 : false),
    });
}

export function useCreateOrder() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: CreateOrderBody) => unwrap(api.POST("/api/orders", { body })),
        onSuccess: () => qc.invalidateQueries({ queryKey: ["orders"] }),
    });
}

export function useUpdateOrderStatus(id: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (v: { status: OrderStatus; idempotencyKey: string; etag?: string }) =>
            unwrap(api.PUT("/api/orders/{id}/status", {
                params: { path: { id }, header: { "Idempotency-Key": v.idempotencyKey } },
                body: { status: v.status },
                headers: v.etag ? { "If-Match": v.etag } : undefined,
            })),
        // Safe to auto-retry network failures: the same Idempotency-Key is reused, so it can't apply twice.
        retry: (count, error) => !(error instanceof ApiError) && count < 2,
        onSuccess: (order) => {
            qc.setQueryData(["order", id], order);
            qc.invalidateQueries({ queryKey: ["orders"] });
        },
        onError: (error) => {
            // 409/412: someone (e.g. the worker) changed it. Refresh so the user sees the current state.
            if (error instanceof ApiError && [409, 412].includes(error.status))
                qc.invalidateQueries({ queryKey: ["order", id] });
        },
    });
}