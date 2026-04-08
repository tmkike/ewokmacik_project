import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { Loan, LoanCreatePayload, LoanReturnPayload } from '../models/loan';

@Injectable({
  providedIn: 'root',
})
export class LoanService {
  // A kölcsönzési műveletek külön REST végponton érhetők el.
  private readonly apiUrl = '/api/loans';

  constructor(private readonly http: HttpClient) {}

  getLoans(): Observable<Loan[]> {
    return this.http.get<Loan[]>(this.apiUrl);
  }

  getActiveLoans(): Observable<Loan[]> {
    return this.http.get<Loan[]>(`${this.apiUrl}/active`);
  }

  getActiveLoanForBook(bookId: string): Observable<Loan | undefined> {
    const params = new HttpParams().set('bookId', bookId);

    return this.http.get<Loan[]>(`${this.apiUrl}/active`, { params }).pipe(
      map((loans) => loans[0]),
    );
  }

  startLoan(payload: LoanCreatePayload): Observable<Loan> {
    return this.http.post<Loan>(this.apiUrl, payload);
  }

  updateLoan(id: string, payload: Omit<LoanCreatePayload, 'bookId'>): Observable<Loan> {
    return this.http.put<Loan>(`${this.apiUrl}/${id}`, payload);
  }

  returnLoan(id: string, payload: LoanReturnPayload = {}): Observable<Loan> {
    return this.http.put<Loan>(`${this.apiUrl}/${id}/return`, payload);
  }
}
