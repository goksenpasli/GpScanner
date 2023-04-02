﻿using System;
using System.Collections.Generic;
using System.Drawing;

namespace PdfiumViewer
{
    /// <summary>
    /// Helper class for searching through PDF documents.
    /// </summary>
    public class PdfSearchManager
    {
        /// <summary>
        /// Creates a new instance of the search manager.
        /// </summary>
        /// <param name="renderer">The renderer to create the search manager for.</param>
        public PdfSearchManager(PdfRenderer renderer)
        {
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));

            HighlightAllMatches = true;
            MatchColor = Color.FromArgb(0x80, Color.Yellow);
            CurrentMatchColor = Color.FromArgb(0x80, SystemColors.Highlight);
        }

        /// <summary>
        /// Gets or sets the border color of the current match.
        /// </summary>
        public Color CurrentMatchBorderColor { get; }

        /// <summary>
        /// Gets or sets the border width of the current match.
        /// </summary>
        public float CurrentMatchBorderWidth { get; }

        /// <summary>
        /// Gets or sets the color of the current match.
        /// </summary>
        public Color CurrentMatchColor { get; }

        /// <summary>
        /// Gets or sets whether all matches should be highlighted.
        /// </summary>
        public bool HighlightAllMatches {
            get => _highlightAllMatches;

            set {
                if (_highlightAllMatches != value)
                {
                    _highlightAllMatches = value;
                    UpdateHighlights();
                }
            }
        }

        /// <summary>
        /// Gets or sets the border color of matched search terms.
        /// </summary>
        public Color MatchBorderColor { get; }

        /// <summary>
        /// Gets or sets the border width of matched search terms.
        /// </summary>
        public float MatchBorderWidth { get; }

        /// <summary>
        /// Gets or sets whether to match case.
        /// </summary>
        public bool MatchCase { get; set; }

        /// <summary>
        /// Gets or sets the color of matched search terms.
        /// </summary>
        public Color MatchColor { get; }

        /// <summary>
        /// Gets or sets whether to match whole words.
        /// </summary>
        public bool MatchWholeWord { get; set; }

        /// <summary>
        /// The renderer associated with the search manager.
        /// </summary>
        public PdfRenderer Renderer { get; }

        /// <summary>
        /// Find the next matched term.
        /// </summary>
        /// <param name="forward">Whether or not to search forward.</param>
        /// <returns>False when the first match was found again; otherwise true.</returns>
        public bool FindNext(bool forward)
        {
            if (_matches == null || _matches.Items.Count == 0)
            {
                return false;
            }

            if (_offset == -1)
            {
                _offset = FindFirstFromCurrentPage();
                _firstMatch = _offset;

                UpdateHighlights();
                ScrollCurrentIntoView();

                return true;
            }

            if (forward)
            {
                _offset++;
                if (_offset >= _matches.Items.Count)
                {
                    _offset = 0;
                }
            }
            else
            {
                _offset--;
                if (_offset < 0)
                {
                    _offset = _matches.Items.Count - 1;
                }
            }

            UpdateHighlights();
            ScrollCurrentIntoView();

            return _offset != _firstMatch;
        }

        /// <summary>
        /// Resets the search manager.
        /// </summary>
        public void Reset()
        {
            _ = Search(null);
        }

        /// <summary>
        /// Searches for the specified text.
        /// </summary>
        /// <param name="text">The text to search.</param>
        /// <returns>Whether any matches were found.</returns>
        public bool Search(string text)
        {
            Renderer.Markers.Clear();

            if (string.IsNullOrEmpty(text))
            {
                _matches = null;
                _bounds = null;
            }
            else
            {
                _matches = Renderer.Document.Search(text, MatchCase, MatchWholeWord);
                _bounds = GetAllBounds();
            }

            _offset = -1;

            UpdateHighlights();

            return _matches?.Items.Count > 0;
        }

        private List<IList<PdfRectangle>> _bounds;

        private int _firstMatch;

        private bool _highlightAllMatches;

        private PdfMatches _matches;

        private int _offset;

        private void AddMatch(int index, bool current)
        {
            foreach (PdfRectangle pdfBounds in _bounds[index])
            {
                RectangleF bounds = new RectangleF(
                    pdfBounds.Bounds.Left - 1,
                    pdfBounds.Bounds.Top + 1,
                    pdfBounds.Bounds.Width + 2,
                    pdfBounds.Bounds.Height - 2
                );

                PdfMarker marker = new PdfMarker(
                    pdfBounds.Page,
                    bounds,
                    current ? CurrentMatchColor : MatchColor,
                    current ? CurrentMatchBorderColor : MatchBorderColor,
                    current ? CurrentMatchBorderWidth : MatchBorderWidth
                );

                Renderer.Markers.Add(marker);
            }
        }

        private int FindFirstFromCurrentPage()
        {
            for (int i = 0; i < Renderer.Document.PageCount; i++)
            {
                int page = (i + Renderer.Page) % Renderer.Document.PageCount;

                for (int j = 0; j < _matches.Items.Count; j++)
                {
                    PdfMatch match = _matches.Items[j];
                    if (match.Page == page)
                    {
                        return j;
                    }
                }
            }

            return 0;
        }

        private List<IList<PdfRectangle>> GetAllBounds()
        {
            List<IList<PdfRectangle>> result = new List<IList<PdfRectangle>>();

            foreach (PdfMatch match in _matches.Items)
            {
                result.Add(Renderer.Document.GetTextBounds(match.TextSpan));
            }

            return result;
        }

        private void ScrollCurrentIntoView()
        {
            IList<PdfRectangle> current = _bounds[_offset];
            if (current.Count > 0)
            {
                Renderer.ScrollIntoView(current[0]);
            }
        }

        private void UpdateHighlights()
        {
            Renderer.Markers.Clear();

            if (_matches == null)
            {
                return;
            }

            if (_highlightAllMatches)
            {
                for (int i = 0; i < _matches.Items.Count; i++)
                {
                    AddMatch(i, i == _offset);
                }
            }
            else if (_offset != -1)
            {
                AddMatch(_offset, true);
            }
        }
    }
}