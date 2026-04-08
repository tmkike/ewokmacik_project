import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { BookDetail } from './book-detail';
import { BookService } from '../../services/book.service';

describe('BookDetail', () => {
  let component: BookDetail;
  let fixture: ComponentFixture<BookDetail>;
  const bookServiceMock = {
    getBook: () => of({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    }),
    updateBook: () => of({}),
    updateAvailability: () => of({}),
    deleteBook: () => of(undefined),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BookDetail],
      providers: [
        provideRouter([]),
        { provide: BookService, useValue: bookServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(BookDetail);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should show success message after a successful save', () => {
    component.book = {
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: true,
    };

    component.saveBook();

    expect(component.successMessage).toBe('A mentés sikeres.');
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
  });
});
