using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Extensions
{
    public enum ScrollDirection
    {
        Vertical,

        Horizontal
    }

    public enum SpacingMode
    {
        /// <summary>
        /// Spacing is disabled and all items will be arranged as closely as possible.
        /// </summary>
        None,

        /// <summary>
        /// The remaining space is evenly distributed between the items on a layout row, as well as the start and end of each row.
        /// </summary>
        Uniform,

        /// <summary>
        /// The remaining space is evenly distributed between the items on a layout row, excluding the start and end of each row.
        /// </summary>
        BetweenItemsOnly,

        /// <summary>
		/// The remaining space is evenly distributed between start and end of each row.
		/// </summary>
        StartAndEndOnly
    }

    public struct ItemRange
    {
        public ItemRange(int startIndex, int endIndex) : this()
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public int EndIndex { get; }

        public int StartIndex { get; }

        public bool Contains(int itemIndex)
        {
            return itemIndex >= StartIndex && itemIndex <= EndIndex;
        }
    }

    public abstract class VirtualizingPanelBase : VirtualizingPanel, IScrollInfo
    {
        public bool CanHorizontallyScroll { get; set; }

        public bool CanVerticallyScroll { get; set; }

        public double ExtentHeight => Extent.Height;

        public double ExtentWidth => Extent.Width;

        public double HorizontalOffset => Offset.X;

        /// <summary>
        /// Mouse wheel delta for pixel based scrolling. The default value is 48 dp.
        /// </summary>
        public double MouseWheelDelta { get; set; } = 48.0;

        /// <summary>
        /// Mouse wheel delta for item based scrolling. The default value is 3 items.
        /// </summary>
        public int MouseWheelDeltaItem { get; set; } = 3;

        /// <summary>
        /// Scroll line delta for pixel based scrolling. The default value is 16 dp.
        /// </summary>
        public double ScrollLineDelta { get; set; } = 16.0;

        public ScrollViewer ScrollOwner { get; set; }

        public double VerticalOffset => Offset.Y;

        public double ViewportHeight => Viewport.Height;

        public double ViewportWidth => Viewport.Width;

        public void LineDown()
        {
            ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? ScrollLineDelta : GetLineDownScrollAmount());
        }

        public void LineLeft()
        {
            ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? -ScrollLineDelta : GetLineLeftScrollAmount());
        }

        public void LineRight()
        {
            ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? ScrollLineDelta : GetLineRightScrollAmount());
        }

        public void LineUp()
        {
            ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? -ScrollLineDelta : GetLineUpScrollAmount());
        }

        public virtual Rect MakeVisible(Visual visual, Rect rectangle)
        {
            Point pos = visual.TransformToAncestor(this).Transform(Offset);

            double scrollAmountX = 0;
            double scrollAmountY = 0;

            if (pos.X < Offset.X)
            {
                scrollAmountX = -(Offset.X - pos.X);
            }
            else if ((pos.X + rectangle.Width) > (Offset.X + Viewport.Width))
            {
                scrollAmountX = pos.X + rectangle.Width - (Offset.X + Viewport.Width);
            }

            if (pos.Y < Offset.Y)
            {
                scrollAmountY = -(Offset.Y - pos.Y);
            }
            else if ((pos.Y + rectangle.Height) > (Offset.Y + Viewport.Height))
            {
                scrollAmountY = pos.Y + rectangle.Height - (Offset.Y + Viewport.Height);
            }

            SetHorizontalOffset(Offset.X + scrollAmountX);

            SetVerticalOffset(Offset.Y + scrollAmountY);

            double visibleRectWidth = Math.Min(rectangle.Width, Viewport.Width);
            double visibleRectHeight = Math.Min(rectangle.Height, Viewport.Height);

            return new Rect(scrollAmountX, scrollAmountY, visibleRectWidth, visibleRectHeight);
        }

        public void MouseWheelDown()
        {
            if (MouseWheelScrollDirection == ScrollDirection.Vertical)
            {
                ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? MouseWheelDelta : GetMouseWheelDownScrollAmount());
            }
            else
            {
                MouseWheelRight();
            }
        }

        public void MouseWheelLeft()
        {
            ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? -MouseWheelDelta : GetMouseWheelLeftScrollAmount());
        }

        public void MouseWheelRight()
        {
            ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? MouseWheelDelta : GetMouseWheelRightScrollAmount());
        }

        public void MouseWheelUp()
        {
            if (MouseWheelScrollDirection == ScrollDirection.Vertical)
            {
                ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? -MouseWheelDelta : GetMouseWheelUpScrollAmount());
            }
            else
            {
                MouseWheelLeft();
            }
        }

        public void PageDown()
        {
            ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? ViewportHeight : GetPageDownScrollAmount());
        }

        public void PageLeft()
        {
            ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? -ViewportHeight : GetPageLeftScrollAmount());
        }

        public void PageRight()
        {
            ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? ViewportHeight : GetPageRightScrollAmount());
        }

        public void PageUp()
        {
            ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? -ViewportHeight : GetPageUpScrollAmount());
        }

        public void SetHorizontalOffset(double offset)
        {
            if (offset < 0 || Viewport.Width >= Extent.Width)
            {
                offset = 0;
            }
            else if (offset + Viewport.Width >= Extent.Width)
            {
                offset = Extent.Width - Viewport.Width;
            }
            Offset = new Point(offset, Offset.Y);
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public void SetVerticalOffset(double offset)
        {
            if (offset < 0 || Viewport.Height >= Extent.Height)
            {
                offset = 0;
            }
            else if (offset + Viewport.Height >= Extent.Height)
            {
                offset = Extent.Height - Viewport.Height;
            }
            Offset = new Point(Offset.X, offset);
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        /// <summary>
        /// The cache length before and after the viewport.
        /// </summary>
        protected VirtualizationCacheLength CacheLength { get; private set; }

        /// <summary>
        /// The Unit of the cache length. Can be Pixel, Item or Page.
        /// When the ItemsOwner is a group item it can only be pixel or item.
        /// </summary>
        protected VirtualizationCacheLengthUnit CacheLengthUnit { get; private set; }

        protected override bool CanHierarchicallyScrollAndVirtualizeCore => true;

        // https://referencesource.microsoft.com/#PresentationFramework/src/Framework/System/Windows/Controls/ScrollViewer.cs,278cafe26a902287,references

        protected Size Extent { get; private set; } = new Size(0, 0);

        /// <summary>
        /// Returns true if the panel is in VirtualizationMode.Recycling, otherwise false.
        /// </summary>
        protected bool IsRecycling => VirtualizationMode == VirtualizationMode.Recycling;

        protected bool IsVirtualizing => GetIsVirtualizing(ItemsControl);

        protected new IRecyclingItemContainerGenerator ItemContainerGenerator
        {
            get
            {
                if (_itemContainerGenerator == null)
                {
                    /* Because of a bug in the framework the ItemContainerGenerator
* is null until InternalChildren accessed at least one time. */
                    _ = InternalChildren;
                    _itemContainerGenerator = (IRecyclingItemContainerGenerator)base.ItemContainerGenerator;
                }
                return _itemContainerGenerator;
            }
        }

        /// <summary>
        /// The range of items that a realized in viewport or cache.
        /// </summary>
        protected ItemRange ItemRange { get; set; }

        protected ReadOnlyCollection<object> Items => ((ItemContainerGenerator)ItemContainerGenerator).Items;

        /// <summary>
        /// The ItemsControl (e.g. ListView).
        /// </summary>
        protected ItemsControl ItemsControl => ItemsControl.GetItemsOwner(this);

        /// <summary>
        /// The ItemsControl (e.g. ListView) or if the ItemsControl is grouping a GroupItem.
        /// </summary>
        protected DependencyObject ItemsOwner
        {
            get
            {
                /* Use reflection to access internal method because the public
 * GetItemsOwner method does always return the itmes control instead
 * of the real items owner for example the group item when grouping */
                _itemsOwner ??= (DependencyObject)typeof(ItemsControl).GetMethod(
                   "GetItemsOwnerInternal",
                   BindingFlags.Static | BindingFlags.NonPublic,
                   null,
                   new Type[] { typeof(DependencyObject) },
                   null
                ).Invoke(null, new object[] { this });
                return _itemsOwner;
            }
        }

        /// <summary>
        /// The direction in which the panel scrolls when user turns the mouse wheel.
        /// </summary>
        protected ScrollDirection MouseWheelScrollDirection { get; set; } = ScrollDirection.Vertical;

        protected Point Offset { get; private set; } = new Point(0, 0);

        // https://referencesource.microsoft.com/#PresentationFramework/src/Framework/System/Windows/Controls/ScrollViewer.cs,278cafe26a902287,references
        protected ScrollUnit ScrollUnit => GetScrollUnit(ItemsControl);

        protected Size Viewport { get; private set; } = new Size(0, 0);

        protected VirtualizationMode VirtualizationMode => GetVirtualizationMode(ItemsControl);

        /// <summary>
        /// Calculates the extent that would be needed to show all items.
        /// </summary>
        protected abstract Size CalculateExtent(Size availableSize);

        protected virtual GeneratorPosition GetGeneratorPositionFromChildIndex(int childIndex)
        {
            return new GeneratorPosition(childIndex, 0);
        }

        protected int GetItemIndexFromChildIndex(int childIndex)
        {
            GeneratorPosition generatorPosition = GetGeneratorPositionFromChildIndex(childIndex);
            return ItemContainerGenerator.IndexFromGeneratorPosition(generatorPosition);
        }

        protected abstract double GetLineDownScrollAmount();

        protected abstract double GetLineLeftScrollAmount();

        protected abstract double GetLineRightScrollAmount();

        protected abstract double GetLineUpScrollAmount();

        protected abstract double GetMouseWheelDownScrollAmount();

        protected abstract double GetMouseWheelLeftScrollAmount();

        protected abstract double GetMouseWheelRightScrollAmount();

        protected abstract double GetMouseWheelUpScrollAmount();

        protected abstract double GetPageDownScrollAmount();

        protected abstract double GetPageLeftScrollAmount();

        protected abstract double GetPageRightScrollAmount();

        protected abstract double GetPageUpScrollAmount();

        protected override Size MeasureOverride(Size availableSize)
        {
            IHierarchicalVirtualizationAndScrollInfo groupItem = ItemsOwner as IHierarchicalVirtualizationAndScrollInfo;

            Size extent;
            Size desiredSize;

            if (groupItem != null)
            {
                /* If the ItemsOwner is a group item the availableSize is ifinity.
                 * Therfore the vieport size provided by the group item is used. */
                Size viewportSize = groupItem.Constraints.Viewport.Size;
                Size headerSize = groupItem.HeaderDesiredSizes.PixelSize;
                double availableWidth = Math.Max(viewportSize.Width - 5, 0); // left margin of 5 dp
                double availableHeight = Math.Max(viewportSize.Height - headerSize.Height, 0);
                availableSize = new Size(availableWidth, availableHeight);

                extent = CalculateExtent(availableSize);

                desiredSize = new Size(extent.Width, extent.Height);
            }
            else
            {
                extent = CalculateExtent(availableSize);

                double desiredWidth = Math.Min(availableSize.Width, extent.Width);
                double desiredHeight = Math.Min(availableSize.Height, extent.Height);
                desiredSize = new Size(desiredWidth, desiredHeight);
            }

            if (groupItem != null)
            {
                Extent = extent;
                Offset = groupItem.Constraints.Viewport.Location;
                Viewport = groupItem.Constraints.Viewport.Size;
                CacheLength = groupItem.Constraints.CacheLength;
                CacheLengthUnit = groupItem.Constraints.CacheLengthUnit; // can be Item or Pixel
            }
            else
            {
                UpdateScrollInfo(desiredSize, extent);
                CacheLength = GetCacheLength(ItemsOwner);
                CacheLengthUnit = GetCacheLengthUnit(ItemsOwner); // can be Page, Item or Pixel
            }

            ItemRange = UpdateItemRange();

            RealizeItems();
            VirtualizeItems();

            return desiredSize;
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;

                case NotifyCollectionChangedAction.Move:
                    RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
                    break;
            }
        }

        /// <summary>
        /// Realizes visible and cached items.
        /// </summary>
        protected virtual void RealizeItems()
        {
            GeneratorPosition startPosition = ItemContainerGenerator.GeneratorPositionFromIndex(ItemRange.StartIndex);

            int childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

            using (ItemContainerGenerator.StartAt(startPosition, GeneratorDirection.Forward, true))
            {
                for (int i = ItemRange.StartIndex; i <= ItemRange.EndIndex; i++, childIndex++)
                {
                    UIElement child = (UIElement)ItemContainerGenerator.GenerateNext(out bool isNewlyRealized);
                    if (isNewlyRealized || /*recycled*/!InternalChildren.Contains(child))
                    {
                        if (childIndex >= InternalChildren.Count)
                        {
                            AddInternalChild(child);
                        }
                        else
                        {
                            InsertInternalChild(childIndex, child);
                        }
                        if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                        {
                            ItemContainerGenerator.PrepareItemContainer(child);
                        }
                        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    }

                    if (child is IHierarchicalVirtualizationAndScrollInfo groupItem)
                    {
                        groupItem.Constraints = new HierarchicalVirtualizationConstraints(
                            new VirtualizationCacheLength(0),
                            VirtualizationCacheLengthUnit.Item,
                            new Rect(0, 0, ViewportWidth, ViewportHeight));
                        child.Measure(new Size(ViewportWidth, ViewportHeight));
                    }
                }
            }
        }

        protected void ScrollHorizontal(double amount)
        {
            SetHorizontalOffset(HorizontalOffset + amount);
        }

        protected void ScrollVertical(double amount)
        {
            SetVerticalOffset(VerticalOffset + amount);
        }

        /// <summary>
        /// Calculates the item range that is visible in the viewport or cached.
        /// </summary>
        protected abstract ItemRange UpdateItemRange();

        protected virtual void UpdateScrollInfo(Size availableSize, Size extent)
        {
            if (ViewportHeight != 0 && VerticalOffset != 0 && VerticalOffset + ViewportHeight + 1 >= ExtentHeight)
            {
                Offset = new Point(Offset.X, extent.Height - availableSize.Height);
                ScrollOwner?.InvalidateScrollInfo();
            }
            if (ViewportWidth != 0 && HorizontalOffset != 0 && HorizontalOffset + ViewportWidth + 1 >= ExtentWidth)
            {
                Offset = new Point(extent.Width - availableSize.Width, Offset.Y);
                ScrollOwner?.InvalidateScrollInfo();
            }
            if (availableSize != Viewport)
            {
                Viewport = availableSize;
                ScrollOwner?.InvalidateScrollInfo();
            }
            if (extent != Extent)
            {
                Extent = extent;
                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        /// <summary>
        /// Virtualizes (cleanups) no longer visible or cached items.
        /// </summary>
        protected virtual void VirtualizeItems()
        {
            for (int childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
            {
                GeneratorPosition generatorPosition = GetGeneratorPositionFromChildIndex(childIndex);

                int itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(generatorPosition);

                if (!ItemRange.Contains(itemIndex))
                {
                    if (VirtualizationMode == VirtualizationMode.Recycling)
                    {
                        ItemContainerGenerator.Recycle(generatorPosition, 1);
                    }
                    else
                    {
                        ItemContainerGenerator.Remove(generatorPosition, 1);
                    }
                    RemoveInternalChildRange(childIndex, 1);
                }
            }
        }

        private IRecyclingItemContainerGenerator _itemContainerGenerator;

        private DependencyObject _itemsOwner;
    }

    public class VirtualizingWrapPanel : VirtualizingPanelBase
    {
        #region Deprecated properties

        [Obsolete("Use ItemSizeProperty")]
        public static readonly DependencyProperty ChildrenSizeProperty = ItemSizeProperty;

        [Obsolete("Use SpacingMode")]
        public static readonly DependencyProperty IsSpacingEnabledProperty = DependencyProperty.Register(nameof(IsSpacingEnabled), typeof(bool), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));

        [Obsolete("Use IsSpacingEnabledProperty")]
        public static readonly DependencyProperty SpacingEnabledProperty = IsSpacingEnabledProperty;

        [Obsolete("Use ItemSize")]
        public Size ChildrenSize { get => ItemSize; set => ItemSize = value; }

        /// <summary>
        ///  Gets or sets a value that specifies whether the items are distributed evenly across the width (horizontal orientation)
        ///  or height (vertical orientation). The default value is true.
        /// </summary>
        [Obsolete("Use SpacingMode")]
        public bool IsSpacingEnabled { get => (bool)GetValue(IsSpacingEnabledProperty); set => SetValue(IsSpacingEnabledProperty, value); }

        [Obsolete("Use IsSpacingEnabled")]
        public bool SpacingEnabled { get => IsSpacingEnabled; set => IsSpacingEnabled = value; }

        #endregion Deprecated properties

        public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(nameof(ItemSize), typeof(Size), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(Size.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure, (obj, _) => ((VirtualizingWrapPanel)obj).Orientation_Changed()));

        public static readonly DependencyProperty SpacingModeProperty = DependencyProperty.Register(nameof(SpacingMode), typeof(SpacingMode), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(SpacingMode.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty StretchItemsProperty = DependencyProperty.Register(nameof(StretchItems), typeof(bool), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));

        /// <summary>
        /// Gets or sets a value that specifies the size of the items. The default value is <see cref="Size.Empty"/>.
        /// If the value is <see cref="Size.Empty"/> the size of the items gots measured by the first realized item.
        /// </summary>
        public Size ItemSize { get => (Size)GetValue(ItemSizeProperty); set => SetValue(ItemSizeProperty, value); }

        /// <summary>
        /// Gets or sets a value that specifies the orientation in which items are arranged. The default value is <see cref="Orientation.Vertical"/>.
        /// </summary>
        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

        /// <summary>
        /// Gets or sets the spacing mode used when arranging the items. The default value is <see cref="SpacingMode.Uniform"/>.
        /// </summary>
        public SpacingMode SpacingMode { get => (SpacingMode)GetValue(SpacingModeProperty); set => SetValue(SpacingModeProperty, value); }

        /// <summary>
        /// Gets or sets a value that specifies if the items get stretched to fill up remaining space. The default value is false.
        /// </summary>
        /// <remarks>
        /// The MaxWidth and MaxHeight properties of the ItemContainerStyle can be used to limit the stretching.
        /// In this case the use of the remaining space will be determined by the SpacingMode property.
        /// </remarks>
        public bool StretchItems { get => (bool)GetValue(StretchItemsProperty); set => SetValue(StretchItemsProperty, value); }

        protected Size childSize;

        protected int itemsPerRowCount;

        protected int rowCount;

        [Obsolete]
        protected override Size ArrangeOverride(Size finalSize)
        {
            double offsetX = GetX(Offset);
            double offsetY = GetY(Offset);

            /* When the items owner is a group item offset is handled by the parent panel. */
            if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo)
            {
                offsetY = 0;
            }

            Size childSize = CalculateChildArrangeSize(finalSize);

            CalculateSpacing(finalSize, out double innerSpacing, out double outerSpacing);

            for (int childIndex = 0; childIndex < InternalChildren.Count; childIndex++)
            {
                UIElement child = InternalChildren[childIndex];

                int itemIndex = GetItemIndexFromChildIndex(childIndex);

                int columnIndex = itemIndex % itemsPerRowCount;
                int rowIndex = itemIndex / itemsPerRowCount;

                double x = outerSpacing + (columnIndex * (GetWidth(childSize) + innerSpacing));
                double y = rowIndex * GetHeight(childSize);

                if (GetHeight(finalSize) == 0.0)
                {
                    /* When the parent panel is grouping and a cached group item is not
                     * in the viewport it has no valid arrangement. That means that the
                     * height/width is 0. Therefore the items should not be visible so
                     * that they are not falsely displayed. */
                    child.Arrange(new Rect(0, 0, 0, 0));
                }
                else
                {
                    child.Arrange(CreateRect(x - offsetX, y - offsetY, childSize.Width, childSize.Height));
                }
            }

            return finalSize;
        }

        protected override void BringIndexIntoView(int index)
        {
            double offset = index / itemsPerRowCount * GetHeight(childSize);
            if (Orientation == Orientation.Horizontal)
            {
                SetHorizontalOffset(offset);
            }
            else
            {
                SetVerticalOffset(offset);
            }
        }

        protected Size CalculateChildArrangeSize(Size finalSize)
        {
            if (StretchItems)
            {
                if (Orientation == Orientation.Vertical)
                {
                    double childMaxWidth = ReadItemContainerStyle(MaxWidthProperty, double.PositiveInfinity);
                    double maxPossibleChildWith = finalSize.Width / itemsPerRowCount;
                    double childWidth = Math.Min(maxPossibleChildWith, childMaxWidth);
                    return new Size(childWidth, childSize.Height);
                }
                else
                {
                    double childMaxHeight = ReadItemContainerStyle(MaxHeightProperty, double.PositiveInfinity);
                    double maxPossibleChildHeight = finalSize.Height / itemsPerRowCount;
                    double childHeight = Math.Min(maxPossibleChildHeight, childMaxHeight);
                    return new Size(childSize.Width, childHeight);
                }
            }
            else
            {
                return childSize;
            }
        }

        [Obsolete]
        protected override Size CalculateExtent(Size availableSize)
        {
            double extentWidth = IsSpacingEnabled && SpacingMode != SpacingMode.None && !double.IsInfinity(GetWidth(availableSize))
                ? GetWidth(availableSize)
                : GetWidth(childSize) * itemsPerRowCount;
            if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo)
            {
                extentWidth = Orientation == Orientation.Vertical
                    ? Math.Max(extentWidth - (Margin.Left + Margin.Right), 0)
                    : Math.Max(extentWidth - (Margin.Top + Margin.Bottom), 0);
            }

            double extentHeight = GetHeight(childSize) * rowCount;
            return CreateSize(extentWidth, extentHeight);
        }

        [Obsolete]
        protected void CalculateSpacing(Size finalSize, out double innerSpacing, out double outerSpacing)
        {
            Size childSize = CalculateChildArrangeSize(finalSize);

            double finalWidth = GetWidth(finalSize);

            double totalItemsWidth = Math.Min(GetWidth(childSize) * itemsPerRowCount, finalWidth);
            double unusedWidth = finalWidth - totalItemsWidth;

            switch (IsSpacingEnabled ? SpacingMode : SpacingMode.None)
            {
                case SpacingMode.Uniform:
                    innerSpacing = outerSpacing = unusedWidth / (itemsPerRowCount + 1);
                    break;

                case SpacingMode.BetweenItemsOnly:
                    innerSpacing = unusedWidth / Math.Max(itemsPerRowCount - 1, 1);
                    outerSpacing = 0;
                    break;

                case SpacingMode.StartAndEndOnly:
                    innerSpacing = 0;
                    outerSpacing = unusedWidth / 2;
                    break;

                case SpacingMode.None:
                default:
                    innerSpacing = 0;
                    outerSpacing = 0;
                    break;
            }
        }

        protected Rect CreateRect(double x, double y, double width, double height)
        {
            return Orientation == Orientation.Vertical ? new Rect(x, y, width, height) : new Rect(y, x, width, height);
        }

        protected Size CreateSize(double width, double height)
        {
            return Orientation == Orientation.Vertical ? new Size(width, height) : new Size(height, width);
        }

        protected double GetHeight(Size size)
        {
            return Orientation == Orientation.Vertical ? size.Height : size.Width;
        }

        protected override double GetLineDownScrollAmount()
        {
            return childSize.Height;
        }

        protected override double GetLineLeftScrollAmount()
        {
            return -childSize.Width;
        }

        protected override double GetLineRightScrollAmount()
        {
            return childSize.Width;
        }

        protected override double GetLineUpScrollAmount()
        {
            return -childSize.Height;
        }

        protected override double GetMouseWheelDownScrollAmount()
        {
            return Math.Min(childSize.Height * MouseWheelDeltaItem, Viewport.Height);
        }

        protected override double GetMouseWheelLeftScrollAmount()
        {
            return -Math.Min(childSize.Width * MouseWheelDeltaItem, Viewport.Width);
        }

        protected override double GetMouseWheelRightScrollAmount()
        {
            return Math.Min(childSize.Width * MouseWheelDeltaItem, Viewport.Width);
        }

        protected override double GetMouseWheelUpScrollAmount()
        {
            return -Math.Min(childSize.Height * MouseWheelDeltaItem, Viewport.Height);
        }

        protected override double GetPageDownScrollAmount()
        {
            return Viewport.Height;
        }

        protected override double GetPageLeftScrollAmount()
        {
            return -Viewport.Width;
        }

        protected override double GetPageRightScrollAmount()
        {
            return Viewport.Width;
        }

        protected override double GetPageUpScrollAmount()
        {
            return -Viewport.Height;
        }

        protected double GetWidth(Size size)
        {
            return Orientation == Orientation.Vertical ? size.Width : size.Height;
        }

        protected double GetX(Point point)
        {
            return Orientation == Orientation.Vertical ? point.X : point.Y;
        }

        protected double GetY(Point point)
        {
            return Orientation == Orientation.Vertical ? point.Y : point.X;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            UpdateChildSize(availableSize);
            return base.MeasureOverride(availableSize);
        }

        protected override ItemRange UpdateItemRange()
        {
            if (!IsVirtualizing)
            {
                return new ItemRange(0, Items.Count - 1);
            }

            int startIndex;
            int endIndex;

            if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo groupItem)
            {
                if (!GetIsVirtualizingWhenGrouping(ItemsControl))
                {
                    return new ItemRange(0, Items.Count - 1);
                }

                Point Offset = new(this.Offset.X, groupItem.Constraints.Viewport.Location.Y);

                int offsetRowIndex;
                double offsetInPixel;

                int rowCountInViewport;
                if (ScrollUnit == ScrollUnit.Item)
                {
                    offsetRowIndex = GetY(Offset) >= 1 ? (int)GetY(Offset) - 1 : 0; // ignore header
                    offsetInPixel = offsetRowIndex * GetHeight(childSize);
                }
                else
                {
                    offsetInPixel = Math.Min(Math.Max(GetY(Offset) - GetHeight(groupItem.HeaderDesiredSizes.PixelSize), 0), GetHeight(Extent));
                    offsetRowIndex = GetRowIndex(offsetInPixel);
                }

                double viewportHeight = Math.Min(GetHeight(Viewport), Math.Max(GetHeight(Extent) - offsetInPixel, 0));

                rowCountInViewport = (int)Math.Ceiling((offsetInPixel + viewportHeight) / GetHeight(childSize)) - (int)Math.Floor(offsetInPixel / GetHeight(childSize));

                startIndex = offsetRowIndex * itemsPerRowCount;
                endIndex = Math.Min(((offsetRowIndex + rowCountInViewport) * itemsPerRowCount) - 1, Items.Count - 1);

                if (CacheLengthUnit == VirtualizationCacheLengthUnit.Pixel)
                {
                    double cacheBeforeInPixel = Math.Min(CacheLength.CacheBeforeViewport, offsetInPixel);
                    double cacheAfterInPixel = Math.Min(CacheLength.CacheAfterViewport, GetHeight(Extent) - viewportHeight - offsetInPixel);
                    int rowCountInCacheBefore = (int)(cacheBeforeInPixel / GetHeight(childSize));
                    int rowCountInCacheAfter = ((int)Math.Ceiling((offsetInPixel + viewportHeight + cacheAfterInPixel) / GetHeight(childSize))) - (int)Math.Ceiling((offsetInPixel + viewportHeight) / GetHeight(childSize));
                    startIndex = Math.Max(startIndex - (rowCountInCacheBefore * itemsPerRowCount), 0);
                    endIndex = Math.Min(endIndex + (rowCountInCacheAfter * itemsPerRowCount), Items.Count - 1);
                }
                else if (CacheLengthUnit == VirtualizationCacheLengthUnit.Item)
                {
                    _ = (int)Math.Ceiling(CacheLength.CacheBeforeViewport / itemsPerRowCount);
                    _ = (int)Math.Ceiling(CacheLength.CacheAfterViewport / itemsPerRowCount);
                    startIndex = Math.Max(startIndex - (int)CacheLength.CacheBeforeViewport, 0);
                    endIndex = Math.Min(endIndex + (int)CacheLength.CacheAfterViewport, Items.Count - 1);
                }
            }
            else
            {
                double viewportSartPos = GetY(Offset);
                double viewportEndPos = GetY(Offset) + GetHeight(Viewport);

                if (CacheLengthUnit == VirtualizationCacheLengthUnit.Pixel)
                {
                    viewportSartPos = Math.Max(viewportSartPos - CacheLength.CacheBeforeViewport, 0);
                    viewportEndPos = Math.Min(viewportEndPos + CacheLength.CacheAfterViewport, GetHeight(Extent));
                }

                int startRowIndex = GetRowIndex(viewportSartPos);
                startIndex = startRowIndex * itemsPerRowCount;

                int endRowIndex = GetRowIndex(viewportEndPos);
                endIndex = Math.Min((endRowIndex * itemsPerRowCount) + (itemsPerRowCount - 1), Items.Count - 1);

                if (CacheLengthUnit == VirtualizationCacheLengthUnit.Page)
                {
                    int itemsPerPage = endIndex - startIndex + 1;
                    startIndex = Math.Max(startIndex - ((int)CacheLength.CacheBeforeViewport * itemsPerPage), 0);
                    endIndex = Math.Min(endIndex + ((int)CacheLength.CacheAfterViewport * itemsPerPage), Items.Count - 1);
                }
                else if (CacheLengthUnit == VirtualizationCacheLengthUnit.Item)
                {
                    startIndex = Math.Max(startIndex - (int)CacheLength.CacheBeforeViewport, 0);
                    endIndex = Math.Min(endIndex + (int)CacheLength.CacheAfterViewport, Items.Count - 1);
                }
            }

            return new ItemRange(startIndex, endIndex);
        }

        private Size CalculateChildSize(Size availableSize)
        {
            if (Items.Count == 0)
            {
                return new Size(0, 0);
            }
            GeneratorPosition startPosition = ItemContainerGenerator.GeneratorPositionFromIndex(0);
            using (ItemContainerGenerator.StartAt(startPosition, GeneratorDirection.Forward, true))
            {
                UIElement child = (UIElement)ItemContainerGenerator.GenerateNext();
                AddInternalChild(child);
                ItemContainerGenerator.PrepareItemContainer(child);
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return child.DesiredSize;
            }
        }

        private int GetRowIndex(double location)
        {
            int calculatedRowIndex = (int)Math.Floor(location / GetHeight(childSize));
            int maxRowIndex = (int)Math.Ceiling(Items.Count / (double)itemsPerRowCount);
            return Math.Max(Math.Min(calculatedRowIndex, maxRowIndex), 0);
        }

        private void Orientation_Changed()
        {
            MouseWheelScrollDirection = Orientation == Orientation.Vertical ? ScrollDirection.Vertical : ScrollDirection.Horizontal;
        }

        private T ReadItemContainerStyle<T>(DependencyProperty property, T fallbackValue = default)
        {
            object value = ItemsControl.ItemContainerStyle?.Setters.OfType<Setter>()
                .FirstOrDefault(setter => setter.Property == property)?.Value;
            return (T)(value ?? fallbackValue);
        }

        private void UpdateChildSize(Size availableSize)
        {
            if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo groupItem
                && GetIsVirtualizingWhenGrouping(ItemsControl))
            {
                if (Orientation == Orientation.Vertical)
                {
                    availableSize.Width = groupItem.Constraints.Viewport.Size.Width;
                    availableSize.Width = Math.Max(availableSize.Width - (Margin.Left + Margin.Right), 0);
                }
                else
                {
                    availableSize.Height = groupItem.Constraints.Viewport.Size.Height;
                    availableSize.Height = Math.Max(availableSize.Height - (Margin.Top + Margin.Bottom), 0);
                }
            }

            childSize = ItemSize != Size.Empty
                ? ItemSize
                : InternalChildren.Count != 0 ? InternalChildren[0].DesiredSize : CalculateChildSize(availableSize);

            itemsPerRowCount = double.IsInfinity(GetWidth(availableSize))
                ? Items.Count
                : Math.Max(1, (int)Math.Floor(GetWidth(availableSize) / GetWidth(childSize)));

            rowCount = (int)Math.Ceiling((double)Items.Count / itemsPerRowCount);
        }

        /* orientation aware helper methods */
    }
}