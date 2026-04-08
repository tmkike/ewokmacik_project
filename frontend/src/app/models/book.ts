import { Loan } from './loan';

export interface Book {
  _id?: string;
  title: string;
  author: string;
  year: number;
  genre: string;
  available: boolean;
  hasActiveLoan?: boolean;
  activeLoan?: Loan | null;
}

export interface BookFilters {
  title?: string;
  genre?: string;
  author?: string;
  available?: boolean | '' | null;
}
