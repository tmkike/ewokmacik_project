import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { BookService } from '../../services/book.service';
import { Add } from './add';

describe('Add', () => {
  let component: Add;
  let fixture: ComponentFixture<Add>;
  let router: Router;

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
    router = TestBed.inject(Router);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should navigate back to the list with success message after add', () => {
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.addBook();

    expect(navigateSpy).toHaveBeenCalledWith(['/books'], {
      queryParams: expect.any(Object),
      state: { systemMessage: 'A könyv hozzáadása sikeres.' },
    });
  });
});
