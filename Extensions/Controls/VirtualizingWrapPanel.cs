using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Extensions
{
    #region VirtualizingWrapPanel

    public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {
        #region OnItemsChanged

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;
            }
        }

        #endregion OnItemsChanged

        #region ItemSize

        #region ItemWidth

        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register("ItemWidth", typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure), IsWidthHeightValid);

        [TypeConverter(typeof(LengthConverter))]
        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);

            set
            {
                if (value <= 0)
                {
                    return;
                }

                SetValue(ItemWidthProperty, value);
            }
        }

        #endregion ItemWidth

        #region ItemHeight

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register("ItemHeight", typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure), IsWidthHeightValid);

        [TypeConverter(typeof(LengthConverter))]
        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);

            set
            {
                if (value <= 0)
                {
                    return;
                }

                SetValue(ItemHeightProperty, value);
            }
        }

        #endregion ItemHeight

        #region IsWidthHeightValid

        private static bool IsWidthHeightValid(object value)
        {
            double d = (double)value;
            return double.IsNaN(d) || (d >= 0 && !double.IsPositiveInfinity(d));
        }

        #endregion IsWidthHeightValid

        #endregion ItemSize

        #region Orientation

        public static readonly DependencyProperty OrientationProperty =
            WrapPanel.OrientationProperty.AddOwner(typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, OnOrientationChanged));

        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not VirtualizingWrapPanel panel)
            {
                return;
            }

            panel._offset = default;
            panel.InvalidateMeasure();
        }

        #endregion Orientation

        #region MeasureOverride, ArrangeOverride

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in InternalChildren)
            {
                ItemContainerGenerator gen = ItemContainerGenerator as ItemContainerGenerator;
                int index = gen?.IndexFromContainer(child) ?? InternalChildren.IndexOf(child);
                if (!_containerLayouts.ContainsKey(index))
                {
                    continue;
                }

                Rect layout = _containerLayouts[index];
                layout.Offset(_offset.X * -1, _offset.Y * -1);
                child.Arrange(layout);
            }

            return finalSize;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _containerLayouts.Clear();
            bool isAutoWidth = double.IsNaN(ItemWidth);
            bool isAutoHeight = double.IsNaN(ItemHeight);
            Size childAvailable = new(isAutoWidth ? double.PositiveInfinity : ItemWidth, isAutoHeight ? double.PositiveInfinity : ItemHeight);
            bool isHorizontal = Orientation == Orientation.Horizontal;
            int childrenCount = InternalChildren.Count;
            ItemsControl itemsControl = ItemsControl.GetItemsOwner(this);
            if (itemsControl != null)
            {
                childrenCount = itemsControl.Items.Count;
            }

            using ChildGenerator generator = new(this);
            double x = 0.0;
            double y = 0.0;
            Size lineSize = default;
            Size maxSize = default;
            for (int i = 0; i < childrenCount; i++)
            {
                Size childSize = ContainerSizeForIndex(i);
                bool isWrapped = isHorizontal ? lineSize.Width + childSize.Width > availableSize.Width : lineSize.Height + childSize.Height > availableSize.Height;
                if (isWrapped)
                {
                    x = isHorizontal ? 0 : x + lineSize.Width;
                    y = isHorizontal ? y + lineSize.Height : 0;
                }

                Rect itemRect = new(x, y, childSize.Width, childSize.Height);
                Rect viewportRect = new(_offset, availableSize);
                if (itemRect.IntersectsWith(viewportRect))
                {
                    UIElement child = generator.GetOrCreateChild(i);
                    child.Measure(childAvailable);
                    childSize = ContainerSizeForIndex(i);
                }

                _containerLayouts[i] = new Rect(x, y, childSize.Width, childSize.Height);
                isWrapped = isHorizontal ? lineSize.Width + childSize.Width > availableSize.Width : lineSize.Height + childSize.Height > availableSize.Height;
                if (isWrapped)
                {
                    maxSize.Width = isHorizontal ? Math.Max(lineSize.Width, maxSize.Width) : maxSize.Width + lineSize.Width;
                    maxSize.Height = isHorizontal ? maxSize.Height + lineSize.Height : Math.Max(lineSize.Height, maxSize.Height);
                    lineSize = childSize;
                    isWrapped = isHorizontal ? childSize.Width > availableSize.Width : childSize.Height > availableSize.Height;
                    if (isWrapped)
                    {
                        maxSize.Width = isHorizontal ? Math.Max(childSize.Width, maxSize.Width) : maxSize.Width + childSize.Width;
                        maxSize.Height = isHorizontal ? maxSize.Height + childSize.Height : Math.Max(childSize.Height, maxSize.Height);
                        lineSize = default;
                    }
                }
                else
                {
                    lineSize.Width = isHorizontal ? lineSize.Width + childSize.Width : Math.Max(childSize.Width, lineSize.Width);
                    lineSize.Height = isHorizontal ? Math.Max(childSize.Height, lineSize.Height) : lineSize.Height + childSize.Height;
                }

                x = isHorizontal ? lineSize.Width : maxSize.Width;
                y = isHorizontal ? maxSize.Height : lineSize.Height;
            }

            maxSize.Width = isHorizontal ? Math.Max(lineSize.Width, maxSize.Width) : maxSize.Width + lineSize.Width;
            maxSize.Height = isHorizontal ? maxSize.Height + lineSize.Height : Math.Max(lineSize.Height, maxSize.Height);
            _extent = maxSize;
            _viewport = availableSize;
            generator.CleanupChildren();
            ScrollOwner?.InvalidateScrollInfo();
            return maxSize;
        }

        private readonly Dictionary<int, Rect> _containerLayouts = new();

        #region ChildGenerator

        private class ChildGenerator : IDisposable
        {
            #region CleanupChildren

            public void CleanupChildren()
            {
                if (_generator == null)
                {
                    return;
                }

                UIElementCollection children = _owner.InternalChildren;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    GeneratorPosition childPos = new(i, 0);
                    int index = _generator.IndexFromGeneratorPosition(childPos);
                    if (index >= _firstGeneratedIndex && index <= _lastGeneratedIndex)
                    {
                        continue;
                    }

                    _generator.Remove(childPos, 1);
                    _owner.RemoveInternalChildRange(i, 1);
                }
            }

            #endregion CleanupChildren

            #region fields

            private readonly IItemContainerGenerator _generator;

            private readonly VirtualizingWrapPanel _owner;

            private int _currentGenerateIndex;

            private int _firstGeneratedIndex;

            private IDisposable _generatorTracker;

            private int _lastGeneratedIndex;

            #endregion fields

            #region _ctor

            public ChildGenerator(VirtualizingWrapPanel owner)
            {
                _owner = owner;
                _ = owner.InternalChildren.Count;
                _generator = owner.ItemContainerGenerator;
            }

            public void Dispose() => _generatorTracker?.Dispose();

            ~ChildGenerator()
            {
                Dispose();
            }

            #endregion _ctor

            #region GetOrCreateChild

            public UIElement GetOrCreateChild(int index)
            {
                if (_generator == null)
                {
                    return _owner.InternalChildren[index];
                }

                if (_generatorTracker == null)
                {
                    BeginGenerate(index);
                }

                UIElement child = _generator.GenerateNext(out bool newlyRealized) as UIElement;
                if (newlyRealized)
                {
                    if (_currentGenerateIndex >= _owner.InternalChildren.Count)
                    {
                        if (child != null)
                        {
                            _owner.AddInternalChild(child);
                        }
                    }
                    else if (child != null)
                    {
                        _owner.InsertInternalChild(_currentGenerateIndex, child);
                    }

                    _generator.PrepareItemContainer(child);
                }

                _lastGeneratedIndex = index;
                _currentGenerateIndex++;
                return child;
            }

            private void BeginGenerate(int index)
            {
                _firstGeneratedIndex = index;
                GeneratorPosition startPos = _generator.GeneratorPositionFromIndex(index);
                _currentGenerateIndex = startPos.Offset == 0 ? startPos.Index : startPos.Index + 1;
                _generatorTracker = _generator.StartAt(startPos, GeneratorDirection.Forward, true);
            }

            #endregion GetOrCreateChild
        }

        #endregion ChildGenerator

        #endregion MeasureOverride, ArrangeOverride

        #region ContainerSizeForIndex

        private Size _prevSize = new(16, 16);

        private Size ContainerSizeForIndex(int index)
        {
            Func<int, Size> getSize = new(idx =>
            {
                UIElement item = null;
                ItemsControl itemsOwner = ItemsControl.GetItemsOwner(this);
                if (itemsOwner == null || ItemContainerGenerator is not ItemContainerGenerator generator)
                {
                    if (InternalChildren.Count > idx)
                    {
                        item = InternalChildren[idx];
                    }
                }
                else
                {
                    if (generator.ContainerFromIndex(idx) != null)
                    {
                        item = generator.ContainerFromIndex(idx) as UIElement;
                    }
                    else if (itemsOwner.Items.Count > idx)
                    {
                        item = itemsOwner.Items[idx] as UIElement;
                    }
                }

                if (item != null)
                {
                    if (item.IsMeasureValid)
                    {
                        return item.DesiredSize;
                    }

                    if (item is FrameworkElement i)
                    {
                        return new Size(i.Width, i.Height);
                    }
                }

                return (_containerLayouts.ContainsKey(idx)) ? _containerLayouts[idx].Size : _prevSize;
            });
            Size size = getSize(index);
            if (!double.IsNaN(ItemWidth))
            {
                size.Width = ItemWidth;
            }

            if (!double.IsNaN(ItemHeight))
            {
                size.Height = ItemHeight;
            }

            return _prevSize = size;
        }

        #endregion ContainerSizeForIndex

        #region IScrollInfo Members

        #region Extent

        public double ExtentHeight => _extent.Height;

        public double ExtentWidth => _extent.Width;

        private Size _extent;

        #endregion Extent

        #region Viewport

        public double ViewportHeight => _viewport.Height;

        public double ViewportWidth => _viewport.Width;

        private Size _viewport;

        #endregion Viewport

        #region Offset

        public double HorizontalOffset => _offset.X;

        public double VerticalOffset => _offset.Y;

        private Point _offset;

        #endregion Offset

        #region ScrollOwner

        public ScrollViewer ScrollOwner { get; set; }

        #endregion ScrollOwner

        #region CanHorizontallyScroll

        public bool CanHorizontallyScroll { get; set; }

        #endregion CanHorizontallyScroll

        #region CanVerticallyScroll

        public bool CanVerticallyScroll { get; set; }

        #endregion CanVerticallyScroll

        #region LineUp

        public void LineUp() => SetVerticalOffset(VerticalOffset - SystemParameters.ScrollHeight);

        #endregion LineUp

        #region LineDown

        public void LineDown() => SetVerticalOffset(VerticalOffset + SystemParameters.ScrollHeight);

        #endregion LineDown

        #region LineLeft

        public void LineLeft() => SetHorizontalOffset(HorizontalOffset - SystemParameters.ScrollWidth);

        #endregion LineLeft

        #region LineRight

        public void LineRight() => SetHorizontalOffset(HorizontalOffset + SystemParameters.ScrollWidth);

        #endregion LineRight

        #region PageUp

        public void PageUp() => SetVerticalOffset(VerticalOffset - _viewport.Height);

        #endregion PageUp

        #region PageDown

        public void PageDown() => SetVerticalOffset(VerticalOffset + _viewport.Height);

        #endregion PageDown

        #region PageLeft

        public void PageLeft() => SetHorizontalOffset(HorizontalOffset - _viewport.Width);

        #endregion PageLeft

        #region PageRight

        public void PageRight() => SetHorizontalOffset(HorizontalOffset + _viewport.Width);

        #endregion PageRight

        #region MouseWheelUp

        public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - (SystemParameters.ScrollHeight * SystemParameters.WheelScrollLines));

        #endregion MouseWheelUp

        #region MouseWheelDown

        public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + (SystemParameters.ScrollHeight * SystemParameters.WheelScrollLines));

        #endregion MouseWheelDown

        #region MouseWheelLeft

        public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - (SystemParameters.ScrollWidth * SystemParameters.WheelScrollLines));

        #endregion MouseWheelLeft

        #region MouseWheelRight

        public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + (SystemParameters.ScrollWidth * SystemParameters.WheelScrollLines));

        #endregion MouseWheelRight

        #region MakeVisible

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            int idx = InternalChildren.IndexOf(visual as UIElement);
            IItemContainerGenerator generator = ItemContainerGenerator;
            if (generator != null)
            {
                GeneratorPosition pos = new(idx, 0);
                idx = generator.IndexFromGeneratorPosition(pos);
            }

            if (idx < 0)
            {
                return Rect.Empty;
            }

            if (!_containerLayouts.ContainsKey(idx))
            {
                return Rect.Empty;
            }

            Rect layout = _containerLayouts[idx];
            if (HorizontalOffset + ViewportWidth < layout.X + layout.Width)
            {
                SetHorizontalOffset(layout.X + layout.Width - ViewportWidth);
            }

            if (layout.X < HorizontalOffset)
            {
                SetHorizontalOffset(layout.X);
            }

            if (VerticalOffset + ViewportHeight < layout.Y + layout.Height)
            {
                SetVerticalOffset(layout.Y + layout.Height - ViewportHeight);
            }

            if (layout.Y < VerticalOffset)
            {
                SetVerticalOffset(layout.Y);
            }

            layout.Width = Math.Min(ViewportWidth, layout.Width);
            layout.Height = Math.Min(ViewportHeight, layout.Height);
            return layout;
        }

        #endregion MakeVisible

        #region SetHorizontalOffset

        public void SetHorizontalOffset(double offset)
        {
            if (offset < 0 || ViewportWidth >= ExtentWidth)
            {
                offset = 0;
            }
            else
            {
                if (offset + ViewportWidth >= ExtentWidth)
                {
                    offset = ExtentWidth - ViewportWidth;
                }
            }

            _offset.X = offset;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        #endregion SetHorizontalOffset

        #region SetVerticalOffset

        public void SetVerticalOffset(double offset)
        {
            if (offset < 0 || ViewportHeight >= ExtentHeight)
            {
                offset = 0;
            }
            else
            {
                if (offset + ViewportHeight >= ExtentHeight)
                {
                    offset = ExtentHeight - ViewportHeight;
                }
            }

            _offset.Y = offset;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        #endregion SetVerticalOffset

        #endregion IScrollInfo Members
    }

    #endregion VirtualizingWrapPanel
}