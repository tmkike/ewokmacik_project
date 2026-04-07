import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

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
  successMessage = '';
  errorMessage = '';

  constructor(private readonly bookService: BookService) {}

  addBook(): void {
    this.saving = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.bookService.addBook(this.book).subscribe({
      next: () => {
        this.successMessage = 'A konyv sikeresen hozzaadva.';
        this.book = this.createEmptyBook();
        this.saving = false;
      },
      error: () => {
        this.errorMessage = 'Nem sikerult hozzaadni a konyvet.';
        this.saving = false;
      },
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
