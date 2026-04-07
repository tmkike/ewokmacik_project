export interface Book {
  _id?: string;
  title: string;
  author: string;
  year: number;
  genre: string;
  available: boolean;
}

export interface BookFilters {
  title?: string;
  genre?: string;
  author?: string;
  available?: boolean | '' | null;
}
