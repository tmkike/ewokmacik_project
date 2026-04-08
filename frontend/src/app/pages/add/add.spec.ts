import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { Add } from './add';
import { BookService } from '../../services/book.service';

describe('Add', () => {
  let component: Add;
  let fixture: ComponentFixture<Add>;
  const bookServiceMock = {
    addBook: () => of({}),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Add],
      providers: [
        provideRouter([]),
        { provide: BookService, useValue: bookServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Add);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
