import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { Loan } from '../../models/loan';
import { BookService } from '../../services/book.service';
import { LoanService } from '../../services/loan.service';
import { BookDetail } from './book-detail';

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
    getActiveLoanForBook: () => of<Loan | undefined>(undefined),
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

  afterEach(() => {
    vi.restoreAllMocks();
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

  it('should expose a borrowed status label when the book has an active loan', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
      hasActiveLoan: true,
    };

    expect(component.bookStatusLabel).toBe('Kikölcsönözve');
  });

  it('should disable the availability checkbox when an active loan is loaded', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
    };

    component['applyLoadedActiveLoan']({
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
    });

    expect(component.book?.available).toBe(false);
    expect(component.isAvailabilityToggleDisabled).toBe(true);
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

  it('should still load active loan details for an unavailable book when loan metadata is missing', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
    };
    loanServiceMock.getActiveLoanForBook = () => of({
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
    });

    component['syncLoanState'](component.book);

    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.showLoanTerminationButton).toBe(true);
    expect(component.isLoanFormReadOnly).toBe(true);
  });

  it('should not start a duplicate active loan lookup for the same book while one is already loading', () => {
    const pendingLookup = new Subject<Loan | undefined>();
    const getActiveLoanForBookSpy = vi.fn(() => pendingLookup.asObservable());

    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
    };
    loanServiceMock.getActiveLoanForBook = getActiveLoanForBookSpy;

    component.loadActiveLoan('1');
    component['syncLoanState'](component.book);

    expect(getActiveLoanForBookSpy).toHaveBeenCalledTimes(1);
    expect(component.loanLoading).toBe(true);

    pendingLookup.next({
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
    });
    pendingLookup.complete();

    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.loanLoading).toBe(false);

    loanServiceMock.getActiveLoanForBook = () => of<Loan | undefined>(undefined);
  });

  it('should clear preloaded book and loan details when the book detail reload fails', () => {
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
        notes: 'Megjegyzés',
        loanedAt: '2026-04-08T10:00:00.000Z',
        dueAt: '2026-04-15T10:00:00.000Z',
        returnedAt: null,
        status: 'active',
      },
    };
    component['applyLoadedActiveLoan'](component.book.activeLoan!);
    bookServiceMock.getBook = () => throwError(() => ({
      error: { message: 'Betöltési hiba' },
    }));

    component.loadBook('1');

    expect(component.book).toBeUndefined();
    expect(component.activeLoan).toBeUndefined();
    expect(component.bookLoadFailed).toBe(true);
    expect(component.loanForm.borrowerName).toBe('');
    expect(component.showLoanTerminationButton).toBe(false);
    expect(component.errorMessage).toBe('Betöltési hiba');

    bookServiceMock.getBook = () => of({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    });
  });

  it('should not perform active loan lookup from stale data when the detail reload fails', () => {
    const getActiveLoanForBookSpy = vi.fn(() => of({
      _id: 'loan-1',
      bookId: '1',
      bookTitle: 'Dune',
      bookAuthor: 'Frank Herbert',
      borrowerName: 'Teszt Elek',
      borrowerEmail: 'teszt@example.com',
      notes: 'Megjegyzés',
      loanedAt: '2026-04-08T10:00:00.000Z',
      dueAt: '2026-04-15T10:00:00.000Z',
      returnedAt: null,
      status: 'active' as const,
    }));

    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
    };
    bookServiceMock.getBook = () => throwError(() => ({
      error: { message: 'Betöltési hiba' },
    }));
    loanServiceMock.getActiveLoanForBook = getActiveLoanForBookSpy;

    component.loadBook('1');

    expect(getActiveLoanForBookSpy).not.toHaveBeenCalled();
    expect(component.book).toBeUndefined();
    expect(component.activeLoan).toBeUndefined();
    expect(component.bookLoadFailed).toBe(true);

    bookServiceMock.getBook = () => of({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    });
    loanServiceMock.getActiveLoanForBook = () => of<Loan | undefined>(undefined);
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

  it('should show inline loan error when loan start fails', () => {
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
    loanServiceMock.startLoan = () => throwError(() => ({
      error: { message: 'A kölcsönzés most nem indítható el.' },
    }));

    component.startLoan();

    expect(component.loanErrorMessage).toBe('A kölcsönzés most nem indítható el.');
    expect(component.errorMessage).toBe('');

    loanServiceMock.startLoan = () => of({
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
    });
  });

  it('should stop loan start and show inline validation when due date is too early', () => {
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
    component.loanForm.dueAt = '2000-01-01';
    const startLoanSpy = vi.spyOn(loanServiceMock, 'startLoan');

    component.saveLoan();

    expect(startLoanSpy).not.toHaveBeenCalled();
    expect(component.loanErrorMessage).toContain('A határidő legkorábban');
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
