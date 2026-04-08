import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { BookDetail } from './book-detail';
import { Loan } from '../../models/loan';
import { BookService } from '../../services/book.service';
import { LoanService } from '../../services/loan.service';

describe('BookDetail', () => {
  let component: BookDetail;
  let fixture: ComponentFixture<BookDetail>;
  let router: Router;

  const bookServiceMock = {
    getBook: () => of({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    }),
    updateBook: () => of({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    }),
    deleteBook: () => of(undefined),
  };

  const loanServiceMock = {
    getActiveLoanForBook: () => of(undefined),
    getActiveLoans: () => of<Loan[]>([]),
    startLoan: () => of({
      _id: 'loan-1',
      bookId: '1',
      bookTitle: 'Dune',
      bookAuthor: 'Frank Herbert',
      borrowerName: 'Teszt Elek',
      borrowerEmail: 'teszt@example.com',
      notes: null,
      loanedAt: '2026-04-08T10:00:00.000Z',
      dueAt: '2026-04-15T10:00:00.000Z',
      returnedAt: null,
      status: 'active' as const,
    }),
    returnLoan: () => of({
      _id: 'loan-1',
      bookId: '1',
      bookTitle: 'Dune',
      bookAuthor: 'Frank Herbert',
      borrowerName: 'Teszt Elek',
      borrowerEmail: 'teszt@example.com',
      notes: null,
      loanedAt: '2026-04-08T10:00:00.000Z',
      dueAt: '2026-04-15T10:00:00.000Z',
      returnedAt: '2026-04-10T10:00:00.000Z',
      status: 'returned' as const,
    }),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BookDetail],
      providers: [
        provideRouter([]),
        { provide: BookService, useValue: bookServiceMock },
        { provide: LoanService, useValue: loanServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(BookDetail);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should show read-only loan state when book payload already contains active loan', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
      hasActiveLoan: true,
      activeLoan: {
        _id: 'loan-1',
        bookId: '1',
        bookTitle: 'Dune',
        bookAuthor: 'Frank Herbert',
        borrowerName: 'Teszt Elek',
        borrowerEmail: 'teszt@example.com',
        notes: null,
        loanedAt: '2026-04-08T10:00:00.000Z',
        dueAt: '2026-04-15T10:00:00.000Z',
        returnedAt: null,
        status: 'active',
      },
    };

    component['syncLoanState'](component.book);

    expect(component.showLoanTerminationButton).toBe(true);
    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.isLoanFormReadOnly).toBe(true);
  });

  it('should keep loan fields editable when the book is unavailable but has no active loan', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
      hasActiveLoan: false,
      activeLoan: null,
    };

    component['syncLoanState'](component.book);

    expect(component.activeLoan).toBeUndefined();
    expect(component.isLoanFormReadOnly).toBe(false);
    expect(component.showLoanTerminationButton).toBe(false);
  });

  it('should navigate back to the list after a successful save', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    };
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.saveBook();

    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A könyv mentése sikeres.' },
    });
    expect(component.errorMessage).toBe('');
  });

  it('should show error message when save fails', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    };

    bookServiceMock.updateBook = () => throwError(() => ({
      error: { message: 'Mentési hiba' },
    }));

    component.saveBook();

    expect(component.successMessage).toBe('');
    expect(component.errorMessage).toBe('Mentési hiba');

    bookServiceMock.updateBook = () => of({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    });
  });

  it('should restore unavailable state when save fails because of active loan conflict', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    };
    bookServiceMock.updateBook = () => throwError(() => ({
      error: { message: 'Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek.' },
    }));

    component.saveBook();

    expect(component.book?.available).toBe(false);
    expect(component.hasActiveLoanConflict).toBe(true);
    expect(component.errorMessage).toBe('Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek.');

    bookServiceMock.updateBook = () => of({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    });
  });

  it('should start a loan and navigate back to the list', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    };
    component.loanForm.borrowerName = 'Teszt Elek';
    component.loanForm.borrowerEmail = 'teszt@example.com';
    component.loanForm.dueAt = '2026-04-15';
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.startLoan();

    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.book?.available).toBe(false);
    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A kölcsönzés sikeresen elindult.' },
    });
  });

  it('should not allow starting a loan when the book is already loaned out', () => {
    component.activeLoan = {
      _id: 'loan-1',
      bookId: '1',
      bookTitle: 'Dune',
      bookAuthor: 'Frank Herbert',
      borrowerName: 'Teszt Elek',
      borrowerEmail: 'teszt@example.com',
      notes: null,
      loanedAt: '2026-04-08T10:00:00.000Z',
      dueAt: '2026-04-15T10:00:00.000Z',
      returnedAt: null,
      status: 'active',
    };
    const startLoanSpy = vi.spyOn(loanServiceMock, 'startLoan');

    component.saveLoan();

    expect(startLoanSpy).not.toHaveBeenCalled();
    expect(component.isLoanFormReadOnly).toBe(true);
  });

  it('should return an active loan and navigate back to the list', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
    };
    component.activeLoan = {
      _id: 'loan-1',
      bookId: '1',
      bookTitle: 'Dune',
      bookAuthor: 'Frank Herbert',
      borrowerName: 'Teszt Elek',
      borrowerEmail: 'teszt@example.com',
      notes: null,
      loanedAt: '2026-04-08T10:00:00.000Z',
      dueAt: '2026-04-15T10:00:00.000Z',
      returnedAt: null,
      status: 'active',
    };
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.returnLoan();

    expect(component.activeLoan).toBeUndefined();
    expect(component.book?.available).toBe(true);
    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A kölcsönzés sikeresen megszüntetve.' },
    });
  });

  it('should load active loan from fallback list when direct lookup returns empty', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
    };
    loanServiceMock.getActiveLoanForBook = () => of(undefined);
    loanServiceMock.getActiveLoans = () => of([{
      _id: 'loan-1',
      bookId: '1',
      bookTitle: 'Dune',
      bookAuthor: 'Frank Herbert',
      borrowerName: 'Teszt Elek',
      borrowerEmail: 'teszt@example.com',
      notes: null,
      loanedAt: '2026-04-08T10:00:00.000Z',
      dueAt: '2026-04-15T10:00:00.000Z',
      returnedAt: null,
      status: 'active' as const,
    }]);

    component.loadActiveLoan('1');

    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.showLoanTerminationButton).toBe(true);
  });
});
