interface Props {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, onPageChange }: Props) {
  const last = Math.max(totalPages, 1);
  return (
    <nav aria-label="Pagination" className="pagination">
      <button type="button" onClick={() => onPageChange(page - 1)} disabled={page <= 1}>
        Previous
      </button>
      <span>
        Page {page} of {last}
      </span>
      <button type="button" onClick={() => onPageChange(page + 1)} disabled={page >= last}>
        Next
      </button>
    </nav>
  );
}
