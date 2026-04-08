import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { Book, BookFilters } from '../models/book';

@Injectable({
  providedIn: 'root',
})
export class BookService {
  // A frontend minden könyves műveletet ezen az egy REST végponton keresztül intéz.
  private readonly apiUrl = 'http://localhost:3000/api/books';

  constructor(private readonly http: HttpClient) {}

  getBooks(filters: BookFilters = {}): Observable<Book[]> {
    let params = new HttpParams();

    if (filters.title) {
      params = params.set('title', filters.title);
    }

    if (filters.genre) {
      params = params.set('genre', filters.genre);
    }

    if (filters.author) {
      params = params.set('author', filters.author);
    }

    if (typeof filters.available === 'boolean') {
      params = params.set('available', String(filters.available));
    }

    return this.http.get<Book[]>(this.apiUrl, { params });
  }

  getBook(id: string): Observable<Book> {
    return this.http.get<Book>(`${this.apiUrl}/${id}`);
  }

  addBook(book: Book): Observable<Book> {
    return this.http.post<Book>(this.apiUrl, book);
  }

  updateBook(id: string, book: Book): Observable<Book> {
    return this.http.put<Book>(`${this.apiUrl}/${id}`, book);
  }

  updateAvailability(id: string, available: boolean): Observable<Book> {
    return this.http.patch<Book>(`${this.apiUrl}/${id}/availability`, { available });
  }

  deleteBook(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
