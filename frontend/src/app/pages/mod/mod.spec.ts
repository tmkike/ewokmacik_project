import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { Mod } from './mod';
import { BookService } from '../../services/book.service';

describe('Mod', () => {
  let component: Mod;
  let fixture: ComponentFixture<Mod>;
  const bookServiceMock = {
    getBooks: () => of({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
    }),
    updateBook: () => of({}),
    updateAvailability: () => of({}),
    deleteBook: () => of(undefined),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Mod],
      providers: [
        { provide: BookService, useValue: bookServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Mod);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
