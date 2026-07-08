// Load @angular/compiler to enable JIT compilation for tests
import '@angular/compiler';
import { getTestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';

// Initialize the Angular testing environment for vitest
getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
