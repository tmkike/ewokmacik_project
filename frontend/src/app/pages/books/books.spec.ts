import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { Book } from '../../models/book';
import { BookService } from '../../services/book.service';
import { Books } from './books';

describe('Books', () => {
  let component: Books;
  let fixture: ComponentFixture<Books>;

  const bookServiceMock = {
    getBooks: vi.fn(() => of({
      items: [] as Book[],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    })),
  };

  beforeEach(async () => {
    bookServiceMock.getBooks.mockClear();

    await TestBed.configureTestingModule({
      imports: [Books],
      providers: [
        provideRouter([]),
        { provide: BookService, useValue: bookServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Books);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load every book immediately on open', () => {
    component.ngOnInit();

    expect(bookServiceMock.getBooks).toHaveBeenCalledWith({
      page: 1,
      pageSize: 50,
    });
    expect(component.books).toEqual([]);
  });

  it('should reset stale filters when the page is opened', () => {
    component.filterForm.setValue({
      title: 'Dune',
      author: 'Frank Herbert',
      genre: 'Science Fiction',
      available: false,
    });

    component.ngOnInit();

    expect(component.filterForm.getRawValue()).toEqual({
      title: '',
      author: '',
      genre: '',
      available: null,
    });
  });

  it('should send the current filters to the backend when searching', () => {
    const filteredBooks = [createBook({ title: 'Dune', author: 'Frank Herbert' })];

    mockBooksResponse(filteredBooks);
    component.filterForm.setValue({
      title: 'Dune',
      author: 'Frank Herbert',
      genre: 'Science',
      available: true,
    });

    component.searchBooks();

    expect(bookServiceMock.getBooks).toHaveBeenCalledWith({
      title: 'Dune',
      author: 'Frank Herbert',
      genre: 'Science',
      available: true,
      page: 1,
      pageSize: 50,
    });
    expect(component.books).toEqual(filteredBooks);
  });

  it('should omit empty text filters and keep the all availability state', () => {
    mockBooksResponse([]);
    component.filterForm.setValue({
      title: '  ',
      author: '',
      genre: '',
      available: null,
    });

    component.searchBooks();

    expect(bookServiceMock.getBooks).toHaveBeenCalledWith({
      page: 1,
      pageSize: 50,
    });
  });

  it('should send the unavailable filter to the backend', () => {
    const filteredBooks = [createBook({ title: '1984', available: false, hasActiveLoan: true })];

    mockBooksResponse(filteredBooks);
    component.filterForm.setValue({
      title: '',
      author: '',
      genre: '',
      available: false,
    });

    component.searchBooks();

    expect(bookServiceMock.getBooks).toHaveBeenCalledWith({
      available: false,
      page: 1,
      pageSize: 50,
    });
    expect(component.books).toEqual(filteredBooks);
  });

  it('should clear filters and reload every book', () => {
    const loadedBooks = [
      createBook({ title: 'Dune', available: true }),
      createBook({ title: '1984', available: false, hasActiveLoan: true }),
    ];

    mockBooksResponse(loadedBooks);
    component.filterForm.setValue({
      title: '1984',
      author: 'George Orwell',
      genre: 'Dystopian',
      available: false,
    });

    component.clearFilters();

    expect(component.filterForm.getRawValue()).toEqual({
      title: '',
      author: '',
      genre: '',
      available: null,
    });
    expect(bookServiceMock.getBooks).toHaveBeenCalledWith({
      page: 1,
      pageSize: 50,
    });
    expect(component.books).toEqual(loadedBooks);
  });

  it('should clear the success message after 1.5 seconds', () => {
    vi.useFakeTimers();
    component.successMessage = 'Sikeres művelet';
    component.successMessageVisible = true;

    (component as any).startSuccessMessageTimer();
    vi.advanceTimersByTime(1500);

    expect(component.successMessage).toBe('');
    expect(component.successMessageVisible).toBe(false);
    vi.useRealTimers();
  });

  it('should clear the navigation message from history', () => {
    const replaceStateSpy = vi.spyOn(history, 'replaceState');

    (component as any).clearNavigationMessageFromHistory();

    expect(replaceStateSpy).toHaveBeenCalled();
  });

  it('should label books with an active loan as borrowed', () => {
    expect(component.getBookAvailabilityLabel(createBook({
      available: false,
      hasActiveLoan: true,
    }))).toBe('Kikölcsönözve');
  });

  it('should keep regular unavailable books distinct from borrowed ones', () => {
    expect(component.getBookAvailabilityLabel(createBook({
      available: false,
      hasActiveLoan: false,
    }))).toBe('Nem elérhető');
  });

  function mockBooksResponse(books: Book[]): void {
    bookServiceMock.getBooks.mockClear();
    bookServiceMock.getBooks.mockReturnValueOnce(of({
      items: books,
      totalCount: books.length,
      page: 1,
      pageSize: 50,
    }));
  }
});

function createBook(overrides: Partial<Book> = {}): Book {
  return {
    _id: 'book-default',
    title: 'Alapértelmezett könyv',
    author: 'Alapértelmezett szerző',
    year: 2000,
    genre: 'Regény',
    available: true,
    hasActiveLoan: false,
    ...overrides,
  };
}
