import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

type Schemas = components["schemas"];
export type Customer = Schemas["CustomerResponse"];
export type CreateCustomerBody = Schemas["CreateCustomerRequest"];
export type Order = Schemas["OrderResponse"];
export type OrderSummary = Schemas["OrderSummaryResponse"];
export type CreateOrderBody = Schemas["CreateOrderRequest"];
export type OrderStatus = Order["status"];

/** RFC 7807 Problem Details as returned by the API. */
export interface Problem {
    title?: string;
    detail?: string;
    status?: number;
    code?: string;
    correlationId?: string;
    errors?: Record<string, string[]>;
}

export class ApiError extends Error {
    readonly status: number;
    readonly problem: Problem;

    constructor(status: number, problem: Problem) {
        super(problem.detail ?? problem.title ?? `Request failed (${status})`);
        this.status = status;
        this.problem = problem;
    }
}

export const api = createClient<paths>({ baseUrl: import.meta.env.VITE_API_BASE_URL ?? "" });

api.use({
    onRequest({ request }) {
        // Dev/mock auth. In production this would come from MSAL (@azure/msal-react) acquireTokenSilent.
        const token = import.meta.env.VITE_DEV_TOKEN;
        if (token) request.headers.set("Authorization", `Bearer ${token}`);
        request.headers.set("X-Correlation-ID", crypto.randomUUID());
        return request;
    },
});

/** Returns data on 2xx, throws ApiError (with Problem Details) otherwise. */
export async function unwrap<T>(
    call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
    const { data, error, response } = await call;
    if (!response.ok) throw new ApiError(response.status, (error ?? {}) as Problem);
    return data as T;
}