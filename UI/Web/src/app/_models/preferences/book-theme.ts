import { ThemeProvider } from "./site-theme";

/**
 * Theme for the the book reader contents
 */
 export interface BookTheme {
    id: number;
    name: string;
    /**
     * The CSS styles to be injected
     */
    contents: string;
    isDefault: boolean;
    provider: ThemeProvider;
  }