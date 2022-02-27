import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Inject, OnDestroy, OnInit, Renderer2, RendererStyleFlags2, ViewChild } from '@angular/core';
import {DOCUMENT, Location} from '@angular/common';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { forkJoin, fromEvent, Subject } from 'rxjs';
import { debounceTime, take, takeUntil } from 'rxjs/operators';
import { Chapter } from 'src/app/_models/chapter';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { NavService } from 'src/app/_services/nav.service';
import { ReaderService } from 'src/app/_services/reader.service';
import { SeriesService } from 'src/app/_services/series.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

import { BookService } from '../book.service';
import { KEY_CODES, UtilityService } from 'src/app/shared/_services/utility.service';
import { BookChapterItem } from '../_models/book-chapter-item';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { Stack } from 'src/app/shared/data-structures/stack';
import { MemberService } from 'src/app/_services/member.service';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';
import { ScrollService } from 'src/app/scroll.service';
import { MangaFormat } from 'src/app/_models/manga-format';
import { LibraryService } from 'src/app/_services/library.service';
import { LibraryType } from 'src/app/_models/library';
import { ThemeService } from 'src/app/theme.service';
import { PageStyle } from '../reader-settings/reader-settings.component';
import { BookTheme } from 'src/app/_models/preferences/book-theme';

enum TabID {
  Settings = 1,
  TableOfContents = 2
}

interface HistoryPoint {
  page: number;
  scrollOffset: number;
}

const TOP_OFFSET = -50 * 1.5; // px the sticky header takes up
const CHAPTER_ID_NOT_FETCHED = -2;
const CHAPTER_ID_DOESNT_EXIST = -1;

/**
 * Styles that should be applied on the top level book-content tag
 */
const pageLevelStyles = ['margin-left', 'margin-right', 'font-size'];
/**
 * Styles that should be applied on every element within book-content tag
 */
const elementLevelStyles = ['line-height', 'font-family'];

@Component({
  selector: 'app-book-reader',
  templateUrl: './book-reader.component.html',
  styleUrls: ['./book-reader.component.scss'],
  animations: [
    trigger('isLoading', [
      state('false', style({opacity: 1})),
      state('true', style({opacity: 0})),
      transition('false <=> true', animate('200ms'))
    ]),
    trigger('fade', [
      state('true', style({opacity: 0})),
      state('false', style({opacity: 0.5})),
      transition('false <=> true', animate('4000ms'))
    ])
  ]
})
export class BookReaderComponent implements OnInit, AfterViewInit, OnDestroy {

  libraryId!: number;
  seriesId!: number;
  volumeId!: number;
  chapterId!: number;
  chapter!: Chapter;
  /**
   * Reading List id. Defaults to -1.
   */
  readingListId: number = CHAPTER_ID_DOESNT_EXIST;

   /**
    * If this is true, no progress will be saved.
    */
  incognitoMode: boolean = false;
 
   /**
    * If this is true, chapters will be fetched in the order of a reading list, rather than natural series order. 
    */
  readingListMode: boolean = false;

  /**
   * The actual pages from the epub, used for showing on table of contents. This must be here as we need access to it for scroll anchors
   */
  chapters: Array<BookChapterItem> = [];
  /**
   * Current Page
   */
  pageNum = 0;
  /**
   * Max Pages
   */
  maxPages = 1;
  /**
   * This allows for exploration into different chapters 
   */
  adhocPageHistory: Stack<HistoryPoint> = new Stack<HistoryPoint>();
  /**
   * A stack of the chapter ids we come across during continuous reading mode. When we traverse a boundary, we use this to avoid extra API calls.
   * @see Stack
   */
  continuousChaptersStack: Stack<number> = new Stack(); // TODO: See if this can be moved into reader service so we can reduce code duplication between readers

  activeTabId: TabID = TabID.Settings;

  drawerOpen = false;
  isLoading = true; 
  bookTitle: string = '';

  clickToPaginate = false;
  /**
   * The boolean that decides if the clickToPaginate overlay is visible or not.
   */
  clickToPaginateVisualOverlay = false;
  clickToPaginateVisualOverlayTimeout: any = undefined; // For animation
  clickToPaginateVisualOverlayTimeout2: any = undefined; // For kicking off animation, giving enough time to render html

  page: SafeHtml | undefined = undefined; // This is the html we get from the server
  styles: SafeHtml | undefined = undefined; // This is the css we get from the server

  @ViewChild('readingHtml', {static: false}) readingHtml!: ElementRef<HTMLDivElement>;
  @ViewChild('readingSection', {static: false}) readingSectionElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('stickyTop', {static: false}) stickyTopElemRef!: ElementRef<HTMLDivElement>;
  @ViewChild('reader', {static: true}) reader!: ElementRef;

  /**
   * Next Chapter Id. This is not garunteed to be a valid ChapterId. Prefetched on page load (non-blocking).
   */
   nextChapterId: number = CHAPTER_ID_NOT_FETCHED;
   /**
    * Previous Chapter Id. This is not garunteed to be a valid ChapterId. Prefetched on page load (non-blocking).
    */
   prevChapterId: number = CHAPTER_ID_NOT_FETCHED;
   /**
    * Is there a next chapter. If not, this will disable UI controls.
    */
   nextChapterDisabled: boolean = false;
   /**
    * Is there a previous chapter. If not, this will disable UI controls.
    */
   prevChapterDisabled: boolean = false;
   /**
    * Has the next chapter been prefetched. Prefetched means the backend will cache the files.
    */
   nextChapterPrefetched: boolean = false;
   /**
    * Has the previous chapter been prefetched. Prefetched means the backend will cache the files.
    */
   prevChapterPrefetched: boolean = false;
  /**
   * If the prev page allows a page change to occur.
   */
   prevPageDisabled = false;
   /**
    * If the next page allows a page change to occur.
    */
   nextPageDisabled = false;

  /**
   * Internal property used to capture all the different css properties to render on all elements. This is a cached version that is updated from reader-settings component
   */
  pageStyles!: PageStyle;

  
  darkMode = false;
  backgroundColor: string = 'white';
  readerStyles: string = '';
  darkModeStyleElem!: HTMLElement;
  topOffset: number = 0; // Offset for drawer and rendering canvas
  /**
   * Used for showing/hiding bottom action bar. Calculates if there is enough scroll to show it.
   * Will hide if all content in book is absolute positioned
   */
  scrollbarNeeded = false;
  readingDirection: ReadingDirection = ReadingDirection.LeftToRight;

  private readonly onDestroy = new Subject<void>();

  pageAnchors: {[n: string]: number } = {};
  currentPageAnchor: string = '';
  /**
   * Last seen progress part path
   */
  lastSeenScrollPartPath: string = '';
  /**
   * Library Type used for rendering chapter or issue
   */
   libraryType: LibraryType = LibraryType.Book;

  /**
   * If the web browser is in fullscreen mode
   */
  isFullscreen: boolean = false;


  darkModeStyles = `
    *:not(input), *:not(select), *:not(code), *:not(:link), *:not(.ngx-toastr) {
        color: #dcdcdc !important;
    }

    code {
        color: #e83e8c !important;
    }

    :link, a {
        color: #8db2e5 !important;
    }

    img, img[src] {
      z-index: 1;
      filter: brightness(0.85) !important;
      background-color: initial !important;
    }

      .reading-section {
        color: #dcdcdc !important;
        background-image: none !important;
        background-color: #010409 !important;
      }
  
      *:not(code), *:not(a) {
          background-color: #010409;
          box-shadow: none;
          text-shadow: none;
          border-radius: unset;
          color: #dcdcdc !important;
      }
  
      *:not(input), *:not(code), *:not(:link) {
          color: #dcdcdc !important;
      }
  
      code {
          color: #e83e8c !important;
      }
  
      .btn-icon {
          background-color: transparent;
      }
  
      :link, a {
          color: #8db2e5 !important;
      }
  
      img, img[src] {
          z-index: 1;
          filter: brightness(0.85) !important;
          background-color: initial !important;
      }
        
      :visited, :visited *, :visited *[class] {color: rgb(211, 138, 138) !important}
      :link:not(cite), :link *:not(cite) {color: #8db2e5 !important}
  `;

  get TabID(): typeof TabID {
    return TabID;
  }

  get ReadingDirection(): typeof ReadingDirection {
    return ReadingDirection;
  }

  get IsPrevDisabled(): boolean {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      // Acting as Previous button
      return this.prevPageDisabled && this.pageNum === 0;
    } else {
      // Acting as a Next button
      return this.nextPageDisabled && this.pageNum + 1 > this.maxPages - 1;
    }
  }

  get IsNextDisabled(): boolean {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      // Acting as Next button
      return this.nextPageDisabled && this.pageNum + 1 > this.maxPages - 1;
    } else {
      // Acting as Previous button
      return this.prevPageDisabled && this.pageNum === 0;
    }
  }

  get IsNextChapter(): boolean {
    return this.pageNum + 1 >= this.maxPages;
  }
  get IsPrevChapter(): boolean {
    return this.pageNum === 0;
  }

  constructor(private route: ActivatedRoute, private router: Router, private accountService: AccountService,
    private seriesService: SeriesService, private readerService: ReaderService, private location: Location,
    private renderer: Renderer2, private navService: NavService, private toastr: ToastrService, 
    private domSanitizer: DomSanitizer, private bookService: BookService, private memberService: MemberService,
    private scrollService: ScrollService, private utilityService: UtilityService, private libraryService: LibraryService,
    @Inject(DOCUMENT) private document: Document, private themeService: ThemeService) {
      this.navService.hideNavBar();
      this.themeService.clearThemes();

      // this.darkModeStyleElem = this.renderer.createElement('style');
      // this.darkModeStyleElem.id = 'brtheme-default';
      // this.renderer.appendChild(this.document.querySelector('.reading-section'), this.darkModeStyleElem)

      // this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      //   if (user) {
      //     this.user = user;
      //   }
      // });
  }

  /**
   * After the page has loaded, setup the scroll handler. The scroll handler has 2 parts. One is if there are page anchors setup (aka page anchor elements linked with the 
   * table of content) then we calculate what has already been reached and grab the last reached one to save progress. If page anchors aren't setup (toc missing), then try to save progress 
   * based on the last seen scroll part (xpath).
   */
  ngAfterViewInit() {
    // this.darkModeStyleElem = this.renderer.createElement('style');
    // this.darkModeStyleElem.id = 'brtheme-default';
    // this.renderer.appendChild(this.document.querySelector('.reading-section'), this.darkModeStyleElem);
    //this.darkModeStyleElem = this.document.querySelector('#brtheme-default');

    // check scroll offset and if offset is after any of the "id" markers, save progress
    fromEvent(this.reader.nativeElement, 'scroll')
      .pipe(debounceTime(200), takeUntil(this.onDestroy)).subscribe((event) => {
        if (this.isLoading) return;

        // Highlight the current chapter we are on
        if (Object.keys(this.pageAnchors).length !== 0) {
          // get the height of the document so we can capture markers that are halfway on the document viewport
          const verticalOffset = this.scrollService.scrollPosition + (this.document.body.offsetHeight / 2);
        
          const alreadyReached = Object.values(this.pageAnchors).filter((i: number) => i <= verticalOffset);
          if (alreadyReached.length > 0) {
            this.currentPageAnchor = Object.keys(this.pageAnchors)[alreadyReached.length - 1];
          } else {
            this.currentPageAnchor = '';
          }
        }

    
        // Find the element that is on screen to bookmark against
        const intersectingEntries = Array.from(this.readingSectionElemRef.nativeElement.querySelectorAll('div,o,p,ul,li,a,img,h1,h2,h3,h4,h5,h6,span'))
                                .filter(element => !element.classList.contains('no-observe'))
                                .filter(entry => {
                                  return this.utilityService.isInViewport(entry, this.topOffset);
                                });

        intersectingEntries.sort(this.sortElements);
        
        if (intersectingEntries.length > 0) {
          let path = this.getXPathTo(intersectingEntries[0]);
            if (path === '') { return; }
            if (!path.startsWith('id')) {
              path = '//html[1]/' + path;
            }
            this.lastSeenScrollPartPath = path;
        }

        if (this.lastSeenScrollPartPath !== '') {
          this.saveProgress();
        }
    });
  }

  saveProgress() {
    let tempPageNum = this.pageNum;
    if (this.pageNum == this.maxPages - 1) {
      tempPageNum = this.pageNum + 1;
    }

    if (!this.incognitoMode) {
      this.readerService.saveProgress(this.seriesId, this.volumeId, this.chapterId, tempPageNum, this.lastSeenScrollPartPath).pipe(take(1)).subscribe(() => {/* No operation */});
    }

  }

  ngOnDestroy(): void {
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout);
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout2);

    this.themeService.currentTheme$.pipe(take(1)).subscribe(theme => {
      this.themeService.setTheme(theme.name);
    });

    this.navService.showNavBar();
    this.readerService.exitFullscreen();

    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnInit(): void {
    const libraryId = this.route.snapshot.paramMap.get('libraryId');
    const seriesId = this.route.snapshot.paramMap.get('seriesId');
    const chapterId = this.route.snapshot.paramMap.get('chapterId');

    if (libraryId === null || seriesId === null || chapterId === null) {
      this.router.navigateByUrl('/library');
      return;
    }


    this.libraryId = parseInt(libraryId, 10);
    this.seriesId = parseInt(seriesId, 10);
    this.chapterId = parseInt(chapterId, 10);
    this.incognitoMode = this.route.snapshot.queryParamMap.get('incognitoMode') === 'true';

    const readingListId = this.route.snapshot.queryParamMap.get('readingListId');
    if (readingListId != null) {
      this.readingListMode = true;
      this.readingListId = parseInt(readingListId, 10);
    }


    this.memberService.hasReadingProgress(this.libraryId).pipe(take(1)).subscribe(hasProgress => {
      if (!hasProgress) {
        this.toggleDrawer();
        this.toastr.info('You can modify book settings, save those settings for all books, and view table of contents from the drawer.');
      }
    });

    this.init();
  }

  init() {
    this.nextChapterId = CHAPTER_ID_NOT_FETCHED;
    this.prevChapterId = CHAPTER_ID_NOT_FETCHED;
    this.nextChapterDisabled = false;
    this.prevChapterDisabled = false;
    this.nextChapterPrefetched = false;

    this.bookService.getBookInfo(this.chapterId).subscribe(info => {
      this.bookTitle = info.bookTitle;
  
      if (this.readingListMode && info.seriesFormat !== MangaFormat.EPUB) {
        // Redirect to the manga reader. 
        const params = this.readerService.getQueryParamsObject(this.incognitoMode, this.readingListMode, this.readingListId);
        this.router.navigate(['library', info.libraryId, 'series', info.seriesId, 'manga', this.chapterId], {queryParams: params});
        return;
      }

      forkJoin({
        chapter: this.seriesService.getChapter(this.chapterId),
        progress: this.readerService.getProgress(this.chapterId),
        chapters: this.bookService.getBookChapters(this.chapterId),
      }).subscribe(results => {
        this.chapter = results.chapter;
        this.volumeId = results.chapter.volumeId;
        this.maxPages = results.chapter.pages;
        this.chapters = results.chapters;
        this.pageNum = results.progress.pageNum;
        
  
        this.continuousChaptersStack.push(this.chapterId);

        this.libraryService.getLibraryType(this.libraryId).pipe(take(1)).subscribe(type => {
          this.libraryType = type;
        });
  
  
  
        if (this.pageNum >= this.maxPages) {
          this.pageNum = this.maxPages - 1;
          this.saveProgress();
        }
  
        this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
          this.nextChapterId = chapterId;
          if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
            this.nextChapterDisabled = true;
          }
        });
        this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
          this.prevChapterId = chapterId;
          if (chapterId === CHAPTER_ID_DOESNT_EXIST || chapterId === this.chapterId) {
            this.prevChapterDisabled = true;
          }
        });
  
        // Check if user progress has part, if so load it so we scroll to it
        this.loadPage(results.progress.bookScrollId || undefined);
      }, () => {
        setTimeout(() => {
          this.closeReader();
        }, 200);
      });
    });

    
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyPress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.RIGHT_ARROW) {
      this.nextPage();
    } else if (event.key === KEY_CODES.LEFT_ARROW) {
      this.prevPage();
    } else if (event.key === KEY_CODES.ESC_KEY) {
      this.closeReader();
    } else if (event.key === KEY_CODES.SPACE) {
      this.toggleDrawer();
      event.stopPropagation();
      event.preventDefault(); 
    } else if (event.key === KEY_CODES.G) {
      this.goToPage();
    } else if (event.key === KEY_CODES.F) {
      this.toggleFullscreen()
    }
  }

  sortElements(a: Element, b: Element) {
    const aTop = a.getBoundingClientRect().top;
      const bTop = b.getBoundingClientRect().top;
      if (aTop < bTop) {
        return -1;
      }
      if (aTop > bTop) {
        return 1;
      }

      return 0;
  }

  loadNextChapter() {
    if (this.nextPageDisabled) { return; }
    this.isLoading = true;
    if (this.nextChapterId === CHAPTER_ID_NOT_FETCHED || this.nextChapterId === this.chapterId) {
      this.readerService.getNextChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.nextChapterId = chapterId;
        this.loadChapter(chapterId, 'Next');
      });
    } else {
      this.loadChapter(this.nextChapterId, 'Next');
    }
  }

  loadPrevChapter() {
    if (this.prevPageDisabled) { return; }
    this.isLoading = true;
    this.continuousChaptersStack.pop();
    const prevChapter = this.continuousChaptersStack.peek();
    if (prevChapter != this.chapterId) {
      if (prevChapter !== undefined) {
        this.chapterId = prevChapter;
        this.init();
        return;
      }
    }

    if (this.prevChapterId === CHAPTER_ID_NOT_FETCHED || this.prevChapterId === this.chapterId) {
      this.readerService.getPrevChapter(this.seriesId, this.volumeId, this.chapterId, this.readingListId).pipe(take(1)).subscribe(chapterId => {
        this.prevChapterId = chapterId;
        this.loadChapter(chapterId, 'Prev');
      });
    } else {
      this.loadChapter(this.prevChapterId, 'Prev');
    }
  }

  loadChapter(chapterId: number, direction: 'Next' | 'Prev') {
    if (chapterId >= 0) {
      this.chapterId = chapterId;
      this.continuousChaptersStack.push(chapterId); 
      // Load chapter Id onto route but don't reload
      const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode, this.readingListMode, this.readingListId);
      window.history.replaceState({}, '', newRoute);
      this.init();
      this.toastr.info(direction + ' ' + this.utilityService.formatChapterName(this.libraryType).toLowerCase() + ' loaded', '', {timeOut: 3000});
    } else {
      // This will only happen if no actual chapter can be found
      this.toastr.warning('Could not find ' + direction.toLowerCase() + ' ' + this.utilityService.formatChapterName(this.libraryType).toLowerCase());
      this.isLoading = false;
      if (direction === 'Prev') {
        this.prevPageDisabled = true;
      } else {
        this.nextPageDisabled = true;
      }
    }
  }

  loadChapterPage(event: {pageNum: number, part: string}) {
    console.log('loading page', event.pageNum);
    this.setPageNum(event.pageNum);
    this.loadPage('id("' + event.part + '")');
  }

  closeReader() {
    if (this.readingListMode) {
      this.router.navigateByUrl('lists/' + this.readingListId);
    } else {
      this.location.back();
    }
  }


  /**
   * Adds a click handler for any anchors that have 'kavita-page'. If 'kavita-page' present, changes page to kavita-page and optionally passes a part value 
   * from 'kavita-part', which will cause the reader to scroll to the marker. 
   */
  addLinkClickHandlers() {
    var links = this.readingSectionElemRef.nativeElement.querySelectorAll('a');
      links.forEach((link: any) => {
        link.addEventListener('click', (e: any) => {
          if (!e.target.attributes.hasOwnProperty('kavita-page')) { return; }
          var page = parseInt(e.target.attributes['kavita-page'].value, 10);
          if (this.adhocPageHistory.peek()?.page !== this.pageNum) {
            this.adhocPageHistory.push({page: this.pageNum, scrollOffset: window.pageYOffset});
          }
          
          var partValue = e.target.attributes.hasOwnProperty('kavita-part') ? e.target.attributes['kavita-part'].value : undefined;
          if (partValue && page === this.pageNum) {
            this.scrollTo(e.target.attributes['kavita-part'].value);
            return;
          }
          
          this.setPageNum(page);
          this.loadPage(partValue);
        });
      });
  }

  moveFocus() {
    const elems = this.document.getElementsByClassName('reading-section');
    if (elems.length > 0) {
      (elems[0] as HTMLDivElement).focus();
    }
  }

  
  promptForPage() {
    const question = 'There are ' + (this.maxPages - 1) + ' pages. What page do you want to go to?';
    const goToPageNum = window.prompt(question, '');
    if (goToPageNum === null || goToPageNum.trim().length === 0) { return null; }
    return goToPageNum;
  }

  goToPage(pageNum?: number) {
    let page = pageNum;
    if (pageNum === null || pageNum === undefined) {
      const goToPageNum = this.promptForPage();
      if (goToPageNum === null) { return; }
      page = parseInt(goToPageNum.trim(), 10);
    }

    if (page === undefined || this.pageNum === page) { return; }

    if (page > this.maxPages) {
      page = this.maxPages;
    } else if (page < 0) {
      page = 0;
    }

    if (!(page === 0 || page === this.maxPages - 1)) {
      page -= 1;
    }

    this.pageNum = page;
    this.loadPage();
  }




  loadPage(part?: string | undefined, scrollTop?: number | undefined) {
    this.isLoading = true;

    this.saveProgress();

    this.bookService.getBookPage(this.chapterId, this.pageNum).pipe(take(1)).subscribe(content => {
      this.page = this.domSanitizer.bypassSecurityTrustHtml(content); // PERF: Potential optimization to prefetch next/prev page and store in localStorage
      
      setTimeout(() => {
        this.addLinkClickHandlers();
        this.updateReaderStyles(this.pageStyles);
        this.topOffset = this.stickyTopElemRef.nativeElement?.offsetHeight;

        const imgs = this.readingSectionElemRef.nativeElement.querySelectorAll('img');
        if (imgs === null || imgs.length === 0) {
          this.setupPage(part, scrollTop);
          return;
        }

        // Apply scaling class to all images to ensure they scale down to max width to not blow out the reader
        Array.from(imgs).forEach(img => this.renderer.addClass(img, 'scale-width'));

        Promise.all(Array.from(imgs)
        .filter(img => !img.complete)
        .map(img => new Promise(resolve => { img.onload = img.onerror = resolve; })))
        .then(() => {
          this.setupPage(part, scrollTop);
        });
      }, 10);
    });
  }

  setupPage(part?: string | undefined, scrollTop?: number | undefined) {
    this.isLoading = false;
    this.scrollbarNeeded = this.readingHtml.nativeElement.clientHeight > this.readingSectionElemRef.nativeElement.clientHeight;

    // Find all the part ids and their top offset
    this.setupPageAnchors();
    

    if (part !== undefined && part !== '') {
      this.scrollTo(part);
    } else if (scrollTop !== undefined && scrollTop !== 0) {
      this.scrollService.scrollTo(scrollTop, this.reader.nativeElement);
    } else {
      this.scrollService.scrollTo(0, this.reader.nativeElement);
    }

    // we need to click the document before arrow keys will scroll down.
    this.reader.nativeElement.focus();
  }


  goBack() {
    if (!this.adhocPageHistory.isEmpty()) {
      const page = this.adhocPageHistory.pop();
      if (page !== undefined) {
        this.setPageNum(page.page);
        this.loadPage(undefined, page.scrollOffset);
      }
    }
  }

  setPageNum(pageNum: number) {
    if (pageNum < 0) {
      this.pageNum = 0;
    } else if (pageNum >= this.maxPages - 1) { // This case handles when we are using the pager to move to the next volume/chapter, the pageNum will get incremented past maxPages // NOTE: I made a change where I removed - 1 in comparison, it's breaking page progress
      this.pageNum = this.maxPages; // 
    } else {
      this.pageNum = pageNum;
    }
  }

  prevPage() {
    const oldPageNum = this.pageNum;
    
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.setPageNum(this.pageNum - 1);
    } else {
      this.setPageNum(this.pageNum + 1);
    }

    if (oldPageNum === 0) {
      // Move to next volume/chapter automatically
      this.loadPrevChapter();
      return;
    }

    if (oldPageNum === this.pageNum) { return; }

    this.loadPage();
  }

  nextPage(event?: any) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    const oldPageNum = this.pageNum;
    if (oldPageNum + 1 === this.maxPages) {
      // Move to next volume/chapter automatically
      this.loadNextChapter();
      return;
    }


    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.setPageNum(this.pageNum + 1);
    } else {
      this.setPageNum(this.pageNum - 1);
    }

    if (oldPageNum === this.pageNum) { return; }

    this.loadPage();
  }

  /**
   * Applies styles onto the html of the book page
   */
  updateReaderStyles(pageStyles: PageStyle) {
    this.pageStyles = pageStyles;
    if (this.readingHtml === undefined || !this.readingHtml.nativeElement)  return;

    console.log('[Style Update] page styles: ', this.pageStyles);

    // Line Height must be placed on each element in the page
    
    // Apply page level overrides
    Object.entries(this.pageStyles).forEach(item => {
      if (item[1] == '100%' || item[1] == '0px' || item[1] == 'inherit') {
        // Remove the style or skip
        this.renderer.removeStyle(this.readingHtml.nativeElement, item[0]);
        return;
      }
      if (pageLevelStyles.includes(item[0])) {
        this.renderer.setStyle(this.readingHtml.nativeElement, item[0], item[1], RendererStyleFlags2.Important);
      }
    });

    const individualElementStyles = Object.entries(this.pageStyles).filter(item => elementLevelStyles.includes(item[0]));
    for(let i = 0; i < this.readingHtml.nativeElement.children.length; i++) {
      const elem = this.readingHtml.nativeElement.children.item(i);
      if (elem?.tagName === 'STYLE') continue;
      individualElementStyles.forEach(item => {
          if (item[1] == '100%' || item[1] == '0px' || item[1] == 'inherit') {
            // Remove the style or skip
            this.renderer.removeStyle(elem, item[0]);
            return;
          }
          this.renderer.setStyle(elem, item[0], item[1], RendererStyleFlags2.Important);
        });
      
    }

  }


  // Color Theme 
  get drawerBackgroundColor() {
    return this.darkMode ? '#010409': '#fff';
  }

  get drawerTextColor() {
    return this.darkMode ? 'white': 'black';
  }

  getDarkModeBackgroundColor() {
    return this.darkMode ? '#292929' : '#fff';
  }

  setOverrideStyles(event: {theme: BookTheme, darkMode: boolean}) {
    this.darkMode = event.darkMode;

    // TODO: Put optimization in to avoid any work if the theme is the same as selected (or have reading settings control handle that)

    // Remove all themes 
    Array.from(this.document.querySelectorAll('style[id^="brtheme-"]')).forEach(elem => elem.remove());


    this.darkModeStyleElem = this.renderer.createElement('style');
    this.darkModeStyleElem.id = event.theme.selector;
    if (event.theme.name === 'Dark') {
      this.darkModeStyleElem.innerHTML = this.darkModeStyles;
    } else {
      this.darkModeStyleElem.innerHTML = '';
    }
    
    console.log('Injected dark styles into book-content')
    this.renderer.appendChild(this.document.querySelector('.reading-section'), this.darkModeStyleElem);
  }

  toggleDrawer() {
    this.topOffset = this.stickyTopElemRef.nativeElement?.offsetHeight;
    this.drawerOpen = !this.drawerOpen;
  }

  scrollTo(partSelector: string) {
    if (partSelector.startsWith('#')) {
      partSelector = partSelector.substr(1, partSelector.length);
    }

    let element = null;
    if (partSelector.startsWith('//') || partSelector.startsWith('id(')) {
      // Part selector is a XPATH
      element = this.getElementFromXPath(partSelector);
    } else {
      element = this.document.querySelector('*[id="' + partSelector + '"]');
    }

    if (element === null) return;

    this.scrollService.scrollTo(element.getBoundingClientRect().top + window.pageYOffset + TOP_OFFSET, this.reader.nativeElement);
  }


  getElementFromXPath(path: string) {
    const node = this.document.evaluate(path, this.document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;
    if (node?.nodeType === Node.ELEMENT_NODE) {
      return node as Element;
    }
    return null;
  }

  getXPathTo(element: any): string {
    if (element === null) return '';
    if (element.id !== '') { return 'id("' + element.id + '")'; }
    if (element === this.document.body) { return element.tagName; }
          
  
    let ix = 0;
    const siblings = element.parentNode?.childNodes || [];
    for (let sibling of siblings) {
        if (sibling === element) {
          return this.getXPathTo(element.parentNode) + '/' + element.tagName + '[' + (ix + 1) + ']';
        }
        if (sibling.nodeType === 1 && sibling.tagName === element.tagName) {
          ix++;
        }
            
    }
    return '';
  }

  /**
   * Turns off Incognito mode. This can only happen once if the user clicks the icon. This will modify URL state
   */
   turnOffIncognito() {
    this.incognitoMode = false;
    const newRoute = this.readerService.getNextChapterUrl(this.router.url, this.chapterId, this.incognitoMode, this.readingListMode, this.readingListId);
    window.history.replaceState({}, '', newRoute);
    this.toastr.info('Incognito mode is off. Progress will now start being tracked.');
    this.saveProgress();
  }

  toggleFullscreen() {
    this.isFullscreen = this.readerService.checkFullscreenMode();
    if (this.isFullscreen) {
      this.readerService.exitFullscreen(() => {
        this.isFullscreen = false;
        this.renderer.removeStyle(this.reader.nativeElement, 'background');
      });
    } else {
      this.readerService.enterFullscreen(this.reader.nativeElement, () => {
        this.isFullscreen = true;
        // HACK: This is a bug with how browsers change the background color for fullscreen mode
        //this.renderer.setStyle(this.reader.nativeElement, 'background', this.themeService.getCssVariable('--bs-body-color')); // This works
        if (!this.darkMode) {
          this.renderer.setStyle(this.reader.nativeElement, 'background', 'white'); // TODO: CHange this to theme background color
        }
      });
    }
  }

  // Table of Contents
  cleanIdSelector(id: string) {
    const tokens = id.split('/');
    if (tokens.length > 0) {
      return tokens[0];
    }
    return id;
  }

  getPageMarkers(ids: Array<string>) {
    try {
      return this.document.querySelectorAll(ids.map(id => '#' + this.cleanIdSelector(id)).join(', '));
    } catch (Exception) {
      // Fallback to anchors instead. Some books have ids that are not valid for querySelectors, so anchors should be used instead
      return this.document.querySelectorAll(ids.map(id => '[href="#' + id + '"]').join(', '));
    }
  }

  setupPageAnchors() {
    this.pageAnchors = {};
    this.currentPageAnchor = '';
    const ids = this.chapters.map(item => item.children).flat().filter(item => item.page === this.pageNum).map(item => item.part).filter(item => item.length > 0);
    if (ids.length > 0) {
      const elems = this.getPageMarkers(ids);
      elems.forEach(elem => {
        this.pageAnchors[elem.id] = elem.getBoundingClientRect().top;
      });
    }
  }

  // Settings Handlers
  showPaginationOverlay(clickToPaginate: boolean) {
    this.clickToPaginate = clickToPaginate;

    // if (this.clickToPaginateVisualOverlayTimeout2 !== undefined) {
    //   clearTimeout(this.clickToPaginateVisualOverlayTimeout2);
    //   this.clickToPaginateVisualOverlayTimeout2 = undefined;
    // }
    this.clearTimeout(this.clickToPaginateVisualOverlayTimeout2);
    if (!clickToPaginate) { return; }

    this.clickToPaginateVisualOverlayTimeout2 = setTimeout(() => {
      this.showClickToPaginateVisualOverlay();
    }, 200);
  }

  clearTimeout(timeoutId: number | undefined) {
    if (timeoutId !== undefined) {
      clearTimeout(timeoutId);
      timeoutId = undefined;
    }
  }

  showClickToPaginateVisualOverlay() {
    this.clickToPaginateVisualOverlay = true;

    if (this.clickToPaginateVisualOverlay && this.clickToPaginateVisualOverlayTimeout !== undefined) {
      clearTimeout(this.clickToPaginateVisualOverlayTimeout);
      this.clickToPaginateVisualOverlayTimeout = undefined;
    }
    this.clickToPaginateVisualOverlayTimeout = setTimeout(() => {
      this.clickToPaginateVisualOverlay = false;
    }, 1000);

  }

  /**
   * Responsible for returning the class to show an overlay or not
   * @param side 
   * @returns 
   */
  clickOverlayClass(side: 'right' | 'left') {
    // TODO: See if we can use RXjs or a component to manage this
    if (!this.clickToPaginateVisualOverlay) {
      return '';
    }

    if (this.readingDirection === ReadingDirection.LeftToRight) {
      return side === 'right' ? 'highlight' : 'highlight-2';
    }
    return side === 'right' ? 'highlight-2' : 'highlight';
  }
}
