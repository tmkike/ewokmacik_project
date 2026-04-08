export interface Loan {
  _id?: string;
  bookId: string;
  bookTitle: string;
  bookAuthor: string;
  borrowerName: string;
  borrowerEmail: string | null;
  notes: string | null;
  loanedAt: string;
  dueAt: string | null;
  returnedAt: string | null;
  status: 'active' | 'returned';
  createdAt?: string;
  updatedAt?: string;
}

export interface LoanCreatePayload {
  bookId: string;
  borrowerName: string;
  borrowerEmail: string;
  notes?: string | null;
  dueAt: string;
}

export interface LoanReturnPayload {
  returnedAt?: string;
}
