import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { Book } from '../../models/book';
import { BookService } from '../../services/book.service';

@Component({
  selector: 'app-add',
  imports: [CommonModule, FormsModule],
  templateUrl: './add.html',
  styleUrl: './add.scss',
})
export class Add {
  book: Book = this.createEmptyBook();
  saving = false;
  errorMessage = '';

  constructor(
    private readonly bookService: BookService,
    private readonly router: Router,
  ) {}

  addBook(): void {
    this.saving = true;
    this.errorMessage = '';

    this.bookService.addBook(this.book).subscribe({
      next: () => {
        this.saving = false;
        // Sikeres mentés után a felhasználó rögtön a friss listára kerül vissza.
        this.navigateToBooks();
      },
      error: () => {
        this.errorMessage = 'Nem sikerült hozzáadni a könyvet.';
        this.saving = false;
      },
    });
  }

  private navigateToBooks(): void {
    void this.router.navigate(['/books'], {
      queryParams: { refresh: Date.now() },
    });
  }

  private createEmptyBook(): Book {
    return {
      title: '',
      author: '',
      year: new Date().getFullYear(),
      genre: '',
      available: true,
    };
  }
}
