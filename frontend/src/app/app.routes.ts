import { Routes } from '@angular/router';
import { MainLayout } from './layout/main-layout/main-layout';
import { Home } from './pages/home/home';
import { Books } from './pages/books/books';
import { Contact } from './pages/contact/contact';
import { Login } from './pages/login/login';
import { Registration } from './pages/registration/registration';

export const routes: Routes = [
  {
    path: '',
    component: MainLayout,
    children: [
      { path: 'home', component: Home },
      { path: 'books', component: Books },
      { path: 'contact', component: Contact },
      { path: 'login', component: Login },
      { path: 'registration', component: Registration }
    ]
  }
];