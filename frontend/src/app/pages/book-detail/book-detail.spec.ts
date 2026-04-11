import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { Book } from '../../models/book';
import { Loan } from '../../models/loan';
import { BookService } from '../../services/book.service';
import { LoanService } from '../../services/loan.service';
import { BOOK_AVAILABILITY_LABELS } from '../../shared/book-availability';
import { BookDetail } from './book-detail';

describe('BookDetail', () => {
  let component: BookDetail;
  let fixture: ComponentFixture<BookDetail>;
  let router: Router;

  const bookServiceMock = {
    getBook: vi.fn((_id: string) => of(createBook({ available: true }))),
    updateBook: vi.fn((_id: string, _book: Book) => of(createBook({ available: true }))),
    deleteBook: vi.fn((_id: string) => of(undefined)),
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

  it('should expose the full available resting state', () => {
    component.book = createBook({
      available: true,
      hasActiveLoan: false,
      activeLoan: null,
    });
    component.activeLoan = undefined;
    component.hasActiveLoanConflict = false;
    component.loanLoading = false;
    component.saving = false;
    component.loanProcessing = false;
    component.bookLoadFailed = false;

    expect(component.availabilityState).toBe('available');
    expect(component.bookStatusLabel).toBe(BOOK_AVAILABILITY_LABELS.available);
    expect(component.showLoanForm).toBe(true);
    expect(component.showLoanTerminationButton).toBe(false);
    expect(component.canDeleteBook).toBe(true);
    expect(component.isLoanFormReadOnly).toBe(false);
  });

  it('should keep delete disabled for a loaned route-state before loan details are loaded', () => {
    component.book = createBook({
      available: false,
      hasActiveLoan: true,
      activeLoan: null,
    });
    component.activeLoan = undefined;
    component.hasActiveLoanConflict = false;
    component.loanLoading = false;
    component.saving = false;
    component.loanProcessing = false;
    component.bookLoadFailed = false;

    expect(component.availabilityState).toBe('loaned');
    expect(component.canDeleteBook).toBe(false);
  });

  it('should keep the loan form hidden while a loaned book awaits its active loan details', () => {
    component.book = createBook({
      available: false,
      hasActiveLoan: true,
      activeLoan: null,
    });
    component.activeLoan = undefined;
    component.hasActiveLoanConflict = false;
    component.loanLoading = false;

    expect(component.showLoanForm).toBe(false);
    expect(component.showLoanTerminationButton).toBe(false);
  });

  it('should expose a borrowed status label when the book has an active loan', () => {
    component.book = createBook({ available: false, hasActiveLoan: true });

    expect(component.bookStatusLabel).toBe(BOOK_AVAILABILITY_LABELS.loaned);
    expect(component.availabilityState).toBe('loaned');
  });

  it('should expose an unavailable status label when the book is manually unavailable', () => {
    component.book = createBook({ available: false, hasActiveLoan: false });

    expect(component.bookStatusLabel).toBe(BOOK_AVAILABILITY_LABELS.unavailable);
    expect(component.availabilityState).toBe('unavailable');
  });

  it('should not start active loan lookup for a manually unavailable book', () => {
    const getActiveLoanForBookSpy = vi.fn(() => of<Loan | undefined>(undefined));

    component.book = createBook({ available: false, hasActiveLoan: false, activeLoan: null });
    loanServiceMock.getActiveLoanForBook = getActiveLoanForBookSpy;

    component['syncLoanState'](component.book);

    expect(getActiveLoanForBookSpy).not.toHaveBeenCalled();
    expect(component.activeLoan).toBeUndefined();
    expect(component.availabilityState).toBe('unavailable');
    expect(component.showLoanForm).toBe(false);

    loanServiceMock.getActiveLoanForBook = () => of<Loan | undefined>(undefined);
  });

  it('should show read-only loan state when book payload already contains active loan', () => {
    component.book = createBook({
      available: false,
      hasActiveLoan: true,
      activeLoan: createActiveLoan(),
    });

    component['syncLoanState'](component.book);

    expect(component.showLoanTerminationButton).toBe(true);
    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.isLoanFormReadOnly).toBe(true);
    expect(component.showLoanForm).toBe(false);
  });

  it('should keep the loan form read-only while loan details are loading', () => {
    component.book = createBook({ available: false, hasActiveLoan: true });
    component.loanLoading = true;

    expect(component.isLoanFormReadOnly).toBe(true);
  });

  it('should load active loan details when the book is loaned but metadata is missing', () => {
    component.book = createBook({ available: false, hasActiveLoan: true });
    loanServiceMock.getActiveLoanForBook = () => of(createActiveLoan());

    component['syncLoanState'](component.book);

    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.showLoanTerminationButton).toBe(true);
    expect(component.availabilityState).toBe('loaned');

    loanServiceMock.getActiveLoanForBook = () => of<Loan | undefined>(undefined);
  });

  it('should allow changing availability locally before save', () => {
    component.book = createBook({ available: true, hasActiveLoan: false });

    component.book.available = false;

    expect(component.book.available).toBe(false);
    expect(component.availabilityState).toBe('unavailable');
  });

  it('should persist the changed availability only when saving the form', () => {
    component.book = createBook({ available: true, hasActiveLoan: false });
    const updateBookSpy = bookServiceMock.updateBook.mockImplementationOnce((_id: string, book: Book) => of(createBook({
      available: book.available ?? true,
      hasActiveLoan: false,
    })));
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.book.available = false;

    expect(updateBookSpy).not.toHaveBeenCalled();

    component.saveBook();

    expect(updateBookSpy).toHaveBeenCalledWith('1', expect.objectContaining({ available: false }));
    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A könyv mentése sikeres.' },
    });
  });

  it('should handle availability conflict during save by switching to the loaned state', () => {
    component.book = createBook({ available: true, hasActiveLoan: false });
    bookServiceMock.updateBook.mockImplementationOnce(() => throwError(() => ({
      error: { message: 'Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek.' },
    })));
    loanServiceMock.getActiveLoanForBook = () => of(createActiveLoan());

    component.saveBook();

    expect(component.book?.available).toBe(false);
    expect(component.availabilityState).toBe('loaned');
    expect(component.errorMessage).toBe('Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek.');
    expect(component.activeLoan?._id).toBe('loan-1');

    loanServiceMock.getActiveLoanForBook = () => of<Loan | undefined>(undefined);
  });

  it('should handle structured active-loan conflict codes during save', () => {
    component.book = createBook({ available: true, hasActiveLoan: false });
    bookServiceMock.updateBook.mockImplementationOnce(() => throwError(() => ({
      error: {
        code: 'ACTIVE_LOAN_CONFLICT',
        message: 'Aktív kölcsönzés mellett a könyv nem jelölhető elérhetőnek.',
      },
    })));
    loanServiceMock.getActiveLoanForBook = () => of(createActiveLoan());

    component.saveBook();

    expect(component.book?.available).toBe(false);
    expect(component.availabilityState).toBe('loaned');
    expect(component.activeLoan?._id).toBe('loan-1');

    loanServiceMock.getActiveLoanForBook = () => of<Loan | undefined>(undefined);
  });

  it('should navigate back to the list after a successful save', () => {
    component.book = createBook({ available: true });
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.saveBook();

    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A könyv mentése sikeres.' },
    });
    expect(component.errorMessage).toBe('');
  });

  it('should start a loan and navigate back to the list', () => {
    component.book = createBook({ available: true });
    component.loanForm.borrowerName = 'Teszt Elek';
    component.loanForm.borrowerEmail = 'teszt@example.com';
    component.loanForm.dueAt = '2026-04-15';
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.startLoan();

    expect(component.activeLoan?._id).toBe('loan-1');
    expect(component.availabilityState).toBe('loaned');
    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A kölcsönzés sikeresen elindult.' },
    });
  });

  it('should not allow starting a loan when the book is not available', () => {
    component.book = createBook({ available: false, hasActiveLoan: false });
    const startLoanSpy = vi.spyOn(loanServiceMock, 'startLoan');

    component.saveLoan();

    expect(startLoanSpy).not.toHaveBeenCalled();
    expect(component.canCreateLoan).toBe(false);
    expect(component.showLoanForm).toBe(false);
  });

  it('should return an active loan and navigate back to the list', () => {
    component.book = createBook({ available: false, hasActiveLoan: true });
    component.activeLoan = createActiveLoan();
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.returnLoan();

    expect(component.activeLoan).toBeUndefined();
    expect(component.book?.available).toBe(true);
    expect(component.availabilityState).toBe('available');
    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A kölcsönzés sikeresen megszüntetve.' },
    });
  });

  it('should not perform active loan lookup from stale data when the detail reload fails', () => {
    const getActiveLoanForBookSpy = vi.fn(() => of(createActiveLoan()));

    component.book = createBook({ available: false, hasActiveLoan: true });
    bookServiceMock.getBook.mockImplementationOnce(() => throwError(() => ({
      error: { message: 'Betöltési hiba' },
    })));
    loanServiceMock.getActiveLoanForBook = getActiveLoanForBookSpy;

    component.loadBook('1');

    expect(getActiveLoanForBookSpy).not.toHaveBeenCalled();
    expect(component.book).toBeUndefined();
    expect(component.activeLoan).toBeUndefined();
    expect(component.bookLoadFailed).toBe(true);

    loanServiceMock.getActiveLoanForBook = () => of<Loan | undefined>(undefined);
  });

  it('should keep API date values on the same calendar day when filling the loan form', () => {
    expect(component['toDateValue']('2026-04-15T00:00:00Z')).toBe('2026-04-15');
  });

  it('should format API date values without timezone drift', () => {
    expect(component.formatLoanDateLabel('2026-04-15T00:00:00Z')).toBe('2026.04.15.');
  });
});

function createBook(overrides: Partial<Book> = {}): Book {
  return {
    _id: '1',
    title: 'Dune',
    author: 'Frank Herbert',
    year: 1965,
    genre: 'Science Fiction',
    available: true,
    hasActiveLoan: false,
    activeLoan: null,
    ...overrides,
  };
}

function createActiveLoan(): Loan {
  return {
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
}
