
import { PageSplitOption } from './page-split-option';
import { READER_MODE } from './reader-mode';
import { ReadingDirection } from './reading-direction';
import { ScalingOption } from './scaling-option';
import { SiteTheme } from './site-theme';

export interface Preferences {
    // Manga Reader
    readingDirection: ReadingDirection;
    scalingOption: ScalingOption;
    pageSplitOption: PageSplitOption;
    readerMode: READER_MODE;
    autoCloseMenu: boolean;
    
    // Book Reader
    bookReaderDarkMode: boolean;
    bookReaderMargin: number;
    bookReaderLineSpacing: number;
    bookReaderFontSize: number;
    bookReaderFontFamily: string;
    bookReaderTapToPaginate: boolean;
    bookReaderReadingDirection: ReadingDirection;

    // Book Reader, new stuff (using ? to avoid having to update other areas )
    bookReaderColorTheme?: string;
    bookReaderLayoutMode?: BookPageLayoutMode;
    /**
     * This mode hides the reading bars and will 
     */
    //bookReaderImmersiveMode?: boolean;

    // Global
    theme: SiteTheme;
}

/**
 * How the content of a book page should render in the reader.
 */
export enum BookPageLayoutMode {
    /**
     * Renders as the book describes
     */
    Original = 1,
    /**
     * Provides virtual paging and breaks a page (usually a long chapter) into subpages that fit to height of device
     */
    SinglePage = 2,
    /**
     * Provides virtual paging and breaks a page (usually a long chapter) into subpages (2 next to each other) that fit to height of device and mimic a real book
     */
    DoublePage = 3
}

export const readingDirections = [{text: 'Left to Right', value: ReadingDirection.LeftToRight}, {text: 'Right to Left', value: ReadingDirection.RightToLeft}];
export const scalingOptions = [{text: 'Automatic', value: ScalingOption.Automatic}, {text: 'Fit to Height', value: ScalingOption.FitToHeight}, {text: 'Fit to Width', value: ScalingOption.FitToWidth}, {text: 'Original', value: ScalingOption.Original}];
export const pageSplitOptions = [{text: 'Fit to Screen', value: PageSplitOption.FitSplit}, {text: 'Right to Left', value: PageSplitOption.SplitRightToLeft}, {text: 'Left to Right', value: PageSplitOption.SplitLeftToRight}, {text: 'No Split', value: PageSplitOption.NoSplit}];
export const readingModes = [{text: 'Left to Right', value: READER_MODE.MANGA_LR}, {text: 'Up to Down', value: READER_MODE.MANGA_UD}, {text: 'Webtoon', value: READER_MODE.WEBTOON}];
