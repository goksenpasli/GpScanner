using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Extensions;

public enum ScrollDirection
{
    /// <summary>
    /// Vertical scroll direction.
    /// </summary>
    Vertical,

    /// <summary>
    /// Horizontal scroll direction.
    /// </summary>
    Horizontal
}

public enum SpacingMode
{
    /// <summary>
    /// Spacing is disabled and all items will be arranged as closely as possible.
    /// </summary>
    None,

    /// <summary>
    /// The remaining space is evenly distributed between the items on a layout row, as well as the start and end of
    /// each row.
    /// </summary>
    Uniform,

    /// <summary>
    /// The remaining space is evenly distributed between the items on a layout row, excluding the start and end of each
    /// row.
    /// </summary>
    BetweenItemsOnly,

    /// <summary>
    /// The remaining space is evenly distributed between start and end of each row.
    /// </summary>
    StartAndEndOnly
}

public readonly struct ItemRange
{
    public ItemRange(int startIndex, int endIndex) : this()
    {
        StartIndex = startIndex;
        EndIndex = endIndex;
    }

    public int EndIndex { get; }

    public int StartIndex { get; }

    public bool Contains(int itemIndex) => itemIndex >= StartIndex && itemIndex <= EndIndex;
}

public abstract class VirtualizingPanelBase : VirtualizingPanel, IScrollInfo
{
    #region Private properties

    /// <summary>
    /// Owner of the displayed items.
    /// </summary>
    private DependencyObject _itemsOwner;

    /// <summary>
    /// Items generator.
    /// </summary>
    private IRecyclingItemContainerGenerator _itemContainerGenerator;

    /// <summary>
    /// Previously set visibility of the vertical scroll bar.
    /// </summary>
    private Visibility _previousVerticalScrollBarVisibility = Visibility.Collapsed;

    /// <summary>
    /// Previously set visibility of the horizontal scroll bar.
    /// </summary>
    private Visibility _previousHorizontalScrollBarVisibility = Visibility.Collapsed;
    #endregion Private properties

    #region Protected properties

    /// <inheritdoc/>
    protected override bool CanHierarchicallyScrollAndVirtualizeCore => true;

    /// <summary>
    /// Gets the scroll unit.
    /// </summary>
    protected ScrollUnit ScrollUnit => GetScrollUnit(ItemsControl);

    /// <summary>
    /// The direction in which the panel scrolls when user turns the mouse wheel.
    /// </summary>
    protected ScrollDirection MouseWheelScrollDirection { get; set; } = ScrollDirection.Vertical;

    /// <summary>
    /// Gets a value that inidicates whether the virtualizing is enabled.
    /// </summary>
    protected bool IsVirtualizing => GetIsVirtualizing(ItemsControl);

    /// <summary>
    /// Gets the virtualization mode.
    /// </summary>
    protected VirtualizationMode VirtualizationMode => GetVirtualizationMode(ItemsControl);

    /// <summary>
    /// Returns true if the panel is in VirtualizationMode.Recycling, otherwise false.
    /// </summary>
    protected bool IsRecycling => VirtualizationMode == VirtualizationMode.Recycling;

    /// <summary>
    /// The cache length before and after the viewport.
    /// </summary>
    protected VirtualizationCacheLength CacheLength { get; private set; }

    /// <summary>
    /// The Unit of the cache length. Can be Pixel, Item or Page. When the ItemsOwner is a group item it can only be
    /// pixel or item.
    /// </summary>
    protected VirtualizationCacheLengthUnit CacheLengthUnit { get; private set; }

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
            if (_itemsOwner is not null)
            {
                return _itemsOwner;
            }

            MethodInfo getItemsOwnerInternalMethod = typeof(ItemsControl).GetMethod("GetItemsOwnerInternal", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(DependencyObject)], null)!;

            _itemsOwner = (DependencyObject)getItemsOwnerInternalMethod.Invoke(null, [this])!;

            return _itemsOwner;
        }
    }

    /// <summary>
    /// Items collection.
    /// </summary>
    protected ReadOnlyCollection<object> Items => ((ItemContainerGenerator)ItemContainerGenerator).Items;

    /// <summary>
    /// Gets the offset.
    /// </summary>
    protected Point Offset { get; private set; } = new(0, 0);

    /// <summary>
    /// Items container.
    /// </summary>
    protected new IRecyclingItemContainerGenerator ItemContainerGenerator
    {
        get
        {
            if (_itemContainerGenerator is not null)
            {
                return _itemContainerGenerator;
            }
            _ = InternalChildren;
            _itemContainerGenerator = (IRecyclingItemContainerGenerator)base.ItemContainerGenerator;

            return _itemContainerGenerator;
        }
    }

    /// <summary>
    /// Gets or sets the range of items that a realized in <see cref="Viewport"/> or cache.
    /// </summary>
    protected ItemRange ItemRange { get; set; }

    /// <summary>
    /// Gets the <see cref="Extent"/>.
    /// </summary>
    protected Size Extent { get; private set; } = new Size(0, 0);

    /// <summary>
    /// Gets the viewport.
    /// </summary>
    protected Size Viewport { get; private set; } = new Size(0, 0);
    #endregion Protected properties

    #region Public properties

    /// <summary>
    /// Property for <see cref="ScrollLineDelta"/>.
    /// </summary>
    public static readonly DependencyProperty ScrollLineDeltaProperty = DependencyProperty.Register(nameof(ScrollLineDelta), typeof(double), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(16.0));

    /// <summary>
    /// Property for <see cref="MouseWheelDelta"/>.
    /// </summary>
    public static readonly DependencyProperty MouseWheelDeltaProperty = DependencyProperty.Register(nameof(MouseWheelDelta), typeof(double), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(48.0));

    /// <summary>
    /// Property for <see cref="ScrollLineDeltaItem"/>.
    /// </summary>
    public static readonly DependencyProperty ScrollLineDeltaItemProperty = DependencyProperty.Register(nameof(ScrollLineDeltaItem), typeof(int), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(1));

    /// <summary>
    /// Property for <see cref="MouseWheelDeltaItem"/>.
    /// </summary>
    public static readonly DependencyProperty MouseWheelDeltaItemProperty = DependencyProperty.Register(nameof(MouseWheelDeltaItem), typeof(int), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(3));

    /// <summary>
    /// Gets or sets the scroll owner.
    /// </summary>
    public ScrollViewer ScrollOwner { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the content can be vertically scrolled.
    /// </summary>
    public bool CanVerticallyScroll { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the content can be horizontally scrolled.
    /// </summary>
    public bool CanHorizontallyScroll { get; set; }

    /// <summary>
    /// Scroll line delta for pixel based scrolling. The default value is 16 dp.
    /// </summary>
    public double ScrollLineDelta { get => (double)GetValue(ScrollLineDeltaProperty); set => SetValue(ScrollLineDeltaProperty, value); }

    /// <summary>
    /// Mouse wheel delta for pixel based scrolling. The default value is 48 dp.
    /// </summary>
    public double MouseWheelDelta { get => (double)GetValue(MouseWheelDeltaProperty); set => SetValue(MouseWheelDeltaProperty, value); }

    /// <summary>
    /// Scroll line delta for item based scrolling. The default value is 1 item.
    /// </summary>
    public double ScrollLineDeltaItem { get => (int)GetValue(ScrollLineDeltaItemProperty); set => SetValue(ScrollLineDeltaItemProperty, value); }

    /// <summary>
    /// Mouse wheel delta for item based scrolling. The default value is 3 items.
    /// </summary>
    public int MouseWheelDeltaItem { get => (int)GetValue(MouseWheelDeltaItemProperty); set => SetValue(MouseWheelDeltaItemProperty, value); }

    /// <summary>
    /// Gets width of the <see cref="Extent"/>.
    /// </summary>
    public double ExtentWidth => Extent.Width;

    /// <summary>
    /// Gets height of the <see cref="Extent"/>.
    /// </summary>
    public double ExtentHeight => Extent.Height;

    /// <summary>
    /// Gets the horizontal offset.
    /// </summary>
    public double HorizontalOffset => Offset.X;

    /// <summary>
    /// Gets the vertical offset.
    /// </summary>
    public double VerticalOffset => Offset.Y;

    /// <summary>
    /// Gets the <see cref="Viewport"/> width.
    /// </summary>
    public double ViewportWidth => Viewport.Width;

    /// <summary>
    /// Gets the <see cref="Viewport"/> height.
    /// </summary>
    public double ViewportHeight => Viewport.Height;
    #endregion Public properties

    #region Public methods

    /// <inheritdoc/>
    public virtual Rect MakeVisible(Visual visual, Rect rectangle)
    {
        Point pos = visual.TransformToAncestor(this).Transform(Offset);

        double scrollAmountX = 0d;
        double scrollAmountY = 0d;

        if (pos.X < Offset.X)
        {
            scrollAmountX = -(Offset.X - pos.X);
        }
        else if ((pos.X + rectangle.Width) > (Offset.X + Viewport.Width))
        {
            double notVisibleX = pos.X + rectangle.Width - (Offset.X + Viewport.Width);
            double maxScrollX = pos.X - Offset.X;
            scrollAmountX = Math.Min(notVisibleX, maxScrollX);
        }

        if (pos.Y < Offset.Y)
        {
            scrollAmountY = -(Offset.Y - pos.Y);
        }
        else if ((pos.Y + rectangle.Height) > (Offset.Y + Viewport.Height))
        {
            double notVisibleY = pos.Y + rectangle.Height - (Offset.Y + Viewport.Height);
            double maxScrollY = pos.Y - Offset.Y;
            scrollAmountY = Math.Min(notVisibleY, maxScrollY);
        }

        SetHorizontalOffset(Offset.X + scrollAmountX);
        SetVerticalOffset(Offset.Y + scrollAmountY);

        double visibleRectWidth = Math.Min(rectangle.Width, Viewport.Width);
        double visibleRectHeight = Math.Min(rectangle.Height, Viewport.Height);

        return new Rect(scrollAmountX, scrollAmountY, visibleRectWidth, visibleRectHeight);
    }

    /// <summary>
    /// Sets the vertical offset.
    /// </summary>
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
    /// Sets the horizontal offset.
    /// </summary>
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

    /// <inheritdoc/>
    public void LineUp() => ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? -ScrollLineDelta : GetLineUpScrollAmount());

    /// <inheritdoc/>
    public void LineDown() => ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? ScrollLineDelta : GetLineDownScrollAmount());

    /// <inheritdoc/>
    public void LineLeft() => ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? -ScrollLineDelta : GetLineLeftScrollAmount());

    /// <inheritdoc/>
    public void LineRight() => ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? ScrollLineDelta : GetLineRightScrollAmount());

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void MouseWheelLeft() => ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? -MouseWheelDelta : GetMouseWheelLeftScrollAmount());

    /// <inheritdoc/>
    public void MouseWheelRight() => ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? MouseWheelDelta : GetMouseWheelRightScrollAmount());

    /// <inheritdoc/>
    public void PageUp() => ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? -ViewportHeight : GetPageUpScrollAmount());

    /// <inheritdoc/>
    public void PageDown() => ScrollVertical(ScrollUnit == ScrollUnit.Pixel ? ViewportHeight : GetPageDownScrollAmount());

    /// <inheritdoc/>
    public void PageLeft() => ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? -ViewportHeight : GetPageLeftScrollAmount());

    /// <inheritdoc/>
    public void PageRight() => ScrollHorizontal(ScrollUnit == ScrollUnit.Pixel ? ViewportHeight : GetPageRightScrollAmount());
    #endregion Public methods

    #region Protected methods

    /// <inheritdoc/>
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
    /// Updates scroll offset, extent and viewport.
    /// </summary>
    protected virtual void UpdateScrollInfo(Size availableSize, Size extent)
    {
        bool invalidateScrollInfo = false;

        if (extent != Extent)
        {
            Extent = extent;
            invalidateScrollInfo = true;
        }

        if (availableSize != Viewport)
        {
            Viewport = availableSize;
            invalidateScrollInfo = true;
        }

        if (ViewportHeight != 0 && VerticalOffset != 0 && VerticalOffset + ViewportHeight + 1 >= ExtentHeight)
        {
            Offset = new Point(Offset.X, extent.Height - availableSize.Height);
            invalidateScrollInfo = true;
        }

        if (ViewportWidth != 0 && HorizontalOffset != 0 && HorizontalOffset + ViewportWidth + 1 >= ExtentWidth)
        {
            Offset = new Point(extent.Width - availableSize.Width, Offset.Y);
            invalidateScrollInfo = true;
        }

        if (invalidateScrollInfo)
        {
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    /// <summary>
    /// Gets item index from the generator.
    /// </summary>
    protected int GetItemIndexFromChildIndex(int childIndex)
    {
        GeneratorPosition generatorPosition = GetGeneratorPositionFromChildIndex(childIndex);
        return ItemContainerGenerator.IndexFromGeneratorPosition(generatorPosition);
    }

    /// <summary>
    /// Gets the position of children from the generator.
    /// </summary>
    protected virtual GeneratorPosition GetGeneratorPositionFromChildIndex(int childIndex) => new(childIndex, 0);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (ScrollOwner != null)
        {
            bool verticalScrollBarGotHidden = ScrollOwner.VerticalScrollBarVisibility == ScrollBarVisibility.Auto &&
            ScrollOwner.ComputedVerticalScrollBarVisibility != Visibility.Visible &&
            ScrollOwner.ComputedVerticalScrollBarVisibility != _previousVerticalScrollBarVisibility;

            bool horizontalScrollBarGotHidden = ScrollOwner.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
            ScrollOwner.ComputedHorizontalScrollBarVisibility != Visibility.Visible &&
            ScrollOwner.ComputedHorizontalScrollBarVisibility != _previousHorizontalScrollBarVisibility;

            _previousVerticalScrollBarVisibility = ScrollOwner.ComputedVerticalScrollBarVisibility;
            _previousHorizontalScrollBarVisibility = ScrollOwner.ComputedHorizontalScrollBarVisibility;

            if ((!ScrollOwner.IsMeasureValid && verticalScrollBarGotHidden) || horizontalScrollBarGotHidden)
            {
                return availableSize;
            }
        }

        Size extent;
        Size desiredSize;

        if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo groupItem)
        {
            Size viewportSize = groupItem.Constraints.Viewport.Size;
            Size headerSize = groupItem.HeaderDesiredSizes.PixelSize;
            double availableWidth = Math.Max(viewportSize.Width - 5, 0);
            double availableHeight = Math.Max(viewportSize.Height - headerSize.Height, 0);
            availableSize = new Size(availableWidth, availableHeight);

            extent = CalculateExtent(availableSize);

            desiredSize = new Size(extent.Width, extent.Height);

            Extent = extent;
            Offset = groupItem.Constraints.Viewport.Location;
            Viewport = groupItem.Constraints.Viewport.Size;
            CacheLength = groupItem.Constraints.CacheLength;
            CacheLengthUnit = groupItem.Constraints.CacheLengthUnit;
        }
        else
        {
            extent = CalculateExtent(availableSize);
            double desiredWidth = Math.Min(availableSize.Width, extent.Width);
            double desiredHeight = Math.Min(availableSize.Height, extent.Height);
            desiredSize = new Size(desiredWidth, desiredHeight);

            UpdateScrollInfo(desiredSize, extent);
            CacheLength = GetCacheLength(ItemsOwner);
            CacheLengthUnit = GetCacheLengthUnit(ItemsOwner);
        }

        ItemRange = UpdateItemRange();

        RealizeItems();
        VirtualizeItems();

        return desiredSize;
    }

    /// <summary>
    /// Realizes visible and cached items.
    /// </summary>
    protected virtual void RealizeItems()
    {
        GeneratorPosition startPosition = ItemContainerGenerator.GeneratorPositionFromIndex(ItemRange.StartIndex);
        int childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

        using IDisposable _ = ItemContainerGenerator.StartAt(startPosition, GeneratorDirection.Forward, true);

        for (int i = ItemRange.StartIndex; i <= ItemRange.EndIndex; i++, childIndex++)
        {
            UIElement child = (UIElement)ItemContainerGenerator.GenerateNext(out bool isNewlyRealized);

            if (isNewlyRealized || !InternalChildren.Contains(child))
            {
                if (childIndex >= InternalChildren.Count)
                {
                    AddInternalChild(child);
                }
                else
                {
                    InsertInternalChild(childIndex, child);
                }

                ItemContainerGenerator.PrepareItemContainer(child);

                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            if (child is not IHierarchicalVirtualizationAndScrollInfo groupItem)
            {
                continue;
            }

            groupItem.Constraints = new HierarchicalVirtualizationConstraints(new VirtualizationCacheLength(0), VirtualizationCacheLengthUnit.Item, new Rect(0, 0, ViewportWidth, ViewportHeight));

            child.Measure(new Size(ViewportWidth, ViewportHeight));
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

            if (itemIndex == -1 || ItemRange.Contains(itemIndex))
            {
                continue;
            }

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

    /// <summary>
    /// Sets vertical scroll offset by given amount.
    /// </summary>
    /// <param name="amount">The value by which the offset is to be increased.</param>
    protected void ScrollVertical(double amount) => SetVerticalOffset(VerticalOffset + amount);

    /// <summary>
    /// Sets horizontal scroll offset by given amount.
    /// </summary>
    /// <param name="amount">The value by which the offset is to be increased.</param>
    protected void ScrollHorizontal(double amount) => SetHorizontalOffset(HorizontalOffset + amount);
    #endregion Protected methods

    #region Protected abstract methods

    /// <summary>
    /// Calculates the extent that would be needed to show all items.
    /// </summary>
    protected abstract Size CalculateExtent(Size availableSize);

    /// <summary>
    /// Calculates the item range that is visible in the viewport or cached.
    /// </summary>
    protected abstract ItemRange UpdateItemRange();

    /// <summary>
    /// Gets line up scroll amount.
    /// </summary>
    protected abstract double GetLineUpScrollAmount();

    /// <summary>
    /// Gets line down scroll amount.
    /// </summary>
    protected abstract double GetLineDownScrollAmount();

    /// <summary>
    /// Gets line left scroll amount.
    /// </summary>
    protected abstract double GetLineLeftScrollAmount();

    /// <summary>
    /// Gets line right scroll amount.
    /// </summary>
    protected abstract double GetLineRightScrollAmount();

    /// <summary>
    /// Gets mouse wheel up scroll amount.
    /// </summary>
    protected abstract double GetMouseWheelUpScrollAmount();

    /// <summary>
    /// Gets mouse wheel down scroll amount.
    /// </summary>
    protected abstract double GetMouseWheelDownScrollAmount();

    /// <summary>
    /// Gets mouse wheel left scroll amount.
    /// </summary>
    protected abstract double GetMouseWheelLeftScrollAmount();

    /// <summary>
    /// Gets mouse wheel right scroll amount.
    /// </summary>
    protected abstract double GetMouseWheelRightScrollAmount();

    /// <summary>
    /// Gets page up scroll amount.
    /// </summary>
    protected abstract double GetPageUpScrollAmount();

    /// <summary>
    /// Gets page down scroll amount.
    /// </summary>
    protected abstract double GetPageDownScrollAmount();

    /// <summary>
    /// Gets page left scroll amount.
    /// </summary>
    protected abstract double GetPageLeftScrollAmount();

    /// <summary>
    /// Gets page right scroll amount.
    /// </summary>
    protected abstract double GetPageRightScrollAmount();
    #endregion Protected abstract methods
}

public class VirtualizingWrapPanel : VirtualizingPanelBase
{
    /// <summary>
    /// Property for <see cref="ItemSize"/>.
    /// </summary>
    public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(nameof(ItemSize), typeof(Size), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(Size.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));
    /// <summary>
    /// Property for <see cref="Orientation"/>.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(Orientation),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure, OnOrientationChanged));
    /// <summary>
    /// Property for <see cref="SpacingMode"/>.
    /// </summary>
    public static readonly DependencyProperty SpacingModeProperty = DependencyProperty.Register(
        nameof(SpacingMode),
        typeof(SpacingMode),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(SpacingMode.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure));
    /// <summary>
    /// Property for <see cref="StretchItems"/>.
    /// </summary>
    public static readonly DependencyProperty StretchItemsProperty = DependencyProperty.Register(nameof(StretchItems), typeof(bool), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));
    /// <summary>
    /// Size of the single child element.
    /// </summary>
    protected Size ChildSize;
    /// <summary>
    /// Amount of displayed items per row.
    /// </summary>
    protected int ItemsPerRowCount;
    /// <summary>
    /// Amount of the displayed rows.
    /// </summary>
    protected int RowCount;

    /// <summary>
    /// Gets or sets a value that specifies the size of the items. The default value is <see cref="Size.Empty"/>. If the
    /// value is <see cref="Size.Empty"/> the size of the items gots measured by the first realized item.
    /// </summary>
    public Size ItemSize { get => (Size)GetValue(ItemSizeProperty); set => SetValue(ItemSizeProperty, value); }

    /// <summary>
    /// Gets or sets a value that specifies the orientation in which items are arranged. The default value is <see
    /// cref="Orientation.Vertical"/>.
    /// </summary>
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>
    /// Gets or sets the spacing mode used when arranging the items. The default value is <see
    /// cref="SpacingMode.Uniform"/>.
    /// </summary>
    public SpacingMode SpacingMode { get => (SpacingMode)GetValue(SpacingModeProperty); set => SetValue(SpacingModeProperty, value); }

    /// <summary>
    /// Gets or sets a value that specifies if the items get stretched to fill up remaining space. The default value is
    /// false.
    /// </summary>
    /// <remarks>
    /// The MaxWidth and MaxHeight properties of the ItemContainerStyle can be used to limit the stretching. In this
    /// case the use of the remaining space will be determined by the SpacingMode property.
    /// </remarks>
    public bool StretchItems { get => (bool)GetValue(StretchItemsProperty); set => SetValue(StretchItemsProperty, value); }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        double offsetX = GetX(Offset);
        double offsetY = GetY(Offset);

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

            int columnIndex = itemIndex % ItemsPerRowCount;
            int rowIndex = itemIndex / ItemsPerRowCount;

            double x = outerSpacing + (columnIndex * (GetWidth(childSize) + innerSpacing));
            double y = rowIndex * GetHeight(childSize);

            if (GetHeight(finalSize) == 0.0)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
            }
            else
            {
                child.Arrange(CreateRect(x - offsetX, y - offsetY, childSize.Width, childSize.Height));
            }
        }

        return finalSize;
    }

    /// <inheritdoc/>
    protected override void BringIndexIntoView(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"The argument {nameof(index)} must be >= 0 and < the number of items.");
        }

        if (ItemsPerRowCount == 0)
        {
            throw new InvalidOperationException();
        }

        double offset = index / ItemsPerRowCount * GetHeight(ChildSize);

        if (Orientation == Orientation.Horizontal)
        {
            SetHorizontalOffset(offset);
        }
        else
        {
            SetVerticalOffset(offset);
        }
    }

    /// <summary>
    /// Calculates desired child arrange size.
    /// </summary>
    protected Size CalculateChildArrangeSize(Size finalSize)
    {
        if (!StretchItems)
        {
            return ChildSize;
        }

        if (Orientation == Orientation.Vertical)
        {
            double childMaxWidth = ReadItemContainerStyle(MaxWidthProperty, double.PositiveInfinity);
            double maxPossibleChildWith = finalSize.Width / ItemsPerRowCount;
            double childWidth = Math.Min(maxPossibleChildWith, childMaxWidth);

            return new Size(childWidth, ChildSize.Height);
        }

        double childMaxHeight = ReadItemContainerStyle(MaxHeightProperty, double.PositiveInfinity);
        double maxPossibleChildHeight = finalSize.Height / ItemsPerRowCount;
        double childHeight = Math.Min(maxPossibleChildHeight, childMaxHeight);

        return new Size(ChildSize.Width, childHeight);
    }

    /// <inheritdoc/>
    protected override Size CalculateExtent(Size availableSize)
    {
        double extentWidth =
            SpacingMode != SpacingMode.None && !double.IsInfinity(GetWidth(availableSize)) ? GetWidth(availableSize) : GetWidth(ChildSize) * ItemsPerRowCount;
        if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo)
        {
            extentWidth = Orientation == Orientation.Vertical ? Math.Max(extentWidth - (Margin.Left + Margin.Right), 0) : Math.Max(extentWidth - (Margin.Top + Margin.Bottom), 0);
        }

        double extentHeight = GetHeight(ChildSize) * RowCount;

        return CreateSize(extentWidth, extentHeight);
    }

    /// <summary>
    /// Calculates desired spacing between items.
    /// </summary>
    protected void CalculateSpacing(Size finalSize, out double innerSpacing, out double outerSpacing)
    {
        Size childSize = CalculateChildArrangeSize(finalSize);

        double finalWidth = GetWidth(finalSize);

        double totalItemsWidth = Math.Min(GetWidth(childSize) * ItemsPerRowCount, finalWidth);

        double unusedWidth = finalWidth - totalItemsWidth;

        switch (SpacingMode)
        {
            case SpacingMode.Uniform:
                innerSpacing = outerSpacing = unusedWidth / (ItemsPerRowCount + 1);
                break;

            case SpacingMode.BetweenItemsOnly:
                innerSpacing = unusedWidth / Math.Max(ItemsPerRowCount - 1, 1);
                outerSpacing = 0;
                break;

            case SpacingMode.StartAndEndOnly:
                innerSpacing = 0;
                outerSpacing = unusedWidth / 2;
                break;
            default:
                innerSpacing = 0;
                outerSpacing = 0;
                break;
        }
    }

    /// <summary>
    /// Defines panel coordinates and size.
    /// </summary>
    protected Rect CreateRect(double x, double y, double width, double height) => Orientation == Orientation.Vertical ? new Rect(x, y, width, height) : new Rect(y, x, width, height);

    /// <summary>
    /// Defines panel size.
    /// </summary>
    protected Size CreateSize(double width, double height) => Orientation == Orientation.Vertical ? new Size(width, height) : new Size(height, width);

    /// <summary>
    /// Gets panel height.
    /// </summary>
    protected double GetHeight(Size size) => Orientation == Orientation.Vertical ? size.Height : size.Width;

    /// <inheritdoc/>
    protected override double GetLineDownScrollAmount() => Math.Min(ChildSize.Height * ScrollLineDeltaItem, Viewport.Height);

    /// <inheritdoc/>
    protected override double GetLineLeftScrollAmount() => -Math.Min(ChildSize.Width * ScrollLineDeltaItem, Viewport.Width);

    /// <inheritdoc/>
    protected override double GetLineRightScrollAmount() => Math.Min(ChildSize.Width * ScrollLineDeltaItem, Viewport.Width);

    /// <inheritdoc/>
    protected override double GetLineUpScrollAmount() => -Math.Min(ChildSize.Height * ScrollLineDeltaItem, Viewport.Height);

    /// <inheritdoc/>
    protected override double GetMouseWheelDownScrollAmount() => Math.Min(ChildSize.Height * MouseWheelDeltaItem, Viewport.Height);

    /// <inheritdoc/>
    protected override double GetMouseWheelLeftScrollAmount() => -Math.Min(ChildSize.Width * MouseWheelDeltaItem, Viewport.Width);

    /// <inheritdoc/>
    protected override double GetMouseWheelRightScrollAmount() => Math.Min(ChildSize.Width * MouseWheelDeltaItem, Viewport.Width);

    /// <inheritdoc/>
    protected override double GetMouseWheelUpScrollAmount() => -Math.Min(ChildSize.Height * MouseWheelDeltaItem, Viewport.Height);

    /// <inheritdoc/>
    protected override double GetPageDownScrollAmount() => Viewport.Height;

    /// <inheritdoc/>
    protected override double GetPageLeftScrollAmount() => -Viewport.Width;

    /// <inheritdoc/>
    protected override double GetPageRightScrollAmount() => Viewport.Width;

    /// <inheritdoc/>
    protected override double GetPageUpScrollAmount() => -Viewport.Height;

    /// <summary>
    /// Gets panel width.
    /// </summary>
    protected double GetWidth(Size size) => Orientation == Orientation.Vertical ? size.Width : size.Height;

    /// <summary>
    /// Gets X panel orientation.
    /// </summary>
    protected double GetX(Point point) => Orientation == Orientation.Vertical ? point.X : point.Y;

    /// <summary>
    /// Gets Y panel orientation.
    /// </summary>
    protected double GetY(Point point) => Orientation == Orientation.Vertical ? point.Y : point.X;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateChildSize(availableSize);

        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// This virtual method is called when <see cref="Orientation"/> is changed.
    /// </summary>
    protected virtual void OnOrientationChanged() => MouseWheelScrollDirection = Orientation == Orientation.Vertical ? ScrollDirection.Vertical : ScrollDirection.Horizontal;

    /// <inheritdoc/>
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

            Point offset = new(Offset.X, groupItem.Constraints.Viewport.Location.Y);

            int offsetRowIndex;
            double offsetInPixel;

            int rowCountInViewport;

            if (ScrollUnit == ScrollUnit.Item)
            {
                offsetRowIndex = GetY(offset) >= 1 ? (int)GetY(offset) - 1 : 0;
                offsetInPixel = offsetRowIndex * GetHeight(ChildSize);
            }
            else
            {
                offsetInPixel = Math.Min(Math.Max(GetY(offset) - GetHeight(groupItem.HeaderDesiredSizes.PixelSize), 0), GetHeight(Extent));
                offsetRowIndex = GetRowIndex(offsetInPixel);
            }

            double viewportHeight = Math.Min(GetHeight(Viewport), Math.Max(GetHeight(Extent) - offsetInPixel, 0));

            rowCountInViewport = (int)Math.Ceiling((offsetInPixel + viewportHeight) / GetHeight(ChildSize)) - (int)Math.Floor(offsetInPixel / GetHeight(ChildSize));

            startIndex = offsetRowIndex * ItemsPerRowCount;
            endIndex = Math.Min(((offsetRowIndex + rowCountInViewport) * ItemsPerRowCount) - 1, Items.Count - 1);

            if (CacheLengthUnit == VirtualizationCacheLengthUnit.Pixel)
            {
                double cacheBeforeInPixel = Math.Min(CacheLength.CacheBeforeViewport, offsetInPixel);
                double cacheAfterInPixel = Math.Min(CacheLength.CacheAfterViewport, GetHeight(Extent) - viewportHeight - offsetInPixel);

                int rowCountInCacheBefore = (int)(cacheBeforeInPixel / GetHeight(ChildSize));
                int rowCountInCacheAfter = ((int)Math.Ceiling((offsetInPixel + viewportHeight + cacheAfterInPixel) / GetHeight(ChildSize))) - (int)Math.Ceiling((offsetInPixel + viewportHeight) / GetHeight(ChildSize));

                startIndex = Math.Max(startIndex - (rowCountInCacheBefore * ItemsPerRowCount), 0);
                endIndex = Math.Min(endIndex + (rowCountInCacheAfter * ItemsPerRowCount), Items.Count - 1);
            }
            else if (CacheLengthUnit == VirtualizationCacheLengthUnit.Item)
            {
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
            startIndex = startRowIndex * ItemsPerRowCount;

            int endRowIndex = GetRowIndex(viewportEndPos);
            endIndex = Math.Min((endRowIndex * ItemsPerRowCount) + (ItemsPerRowCount - 1), Items.Count - 1);

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

    /// <summary>
    /// Private callback for <see cref="OrientationProperty"/>.
    /// </summary>
    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not VirtualizingWrapPanel panel)
        {
            return;
        }

        panel.OnOrientationChanged();
    }

    /// <summary>
    /// Calculates child size.
    /// </summary>
    private Size CalculateChildSize(Size availableSize)
    {
        if (Items.Count == 0)
        {
            return new Size(0, 0);
        }

        GeneratorPosition startPosition = ItemContainerGenerator.GeneratorPositionFromIndex(0);

        using IDisposable _ = ItemContainerGenerator.StartAt(startPosition, GeneratorDirection.Forward, true);

        UIElement child = (UIElement)ItemContainerGenerator.GenerateNext();
        AddInternalChild(child);
        ItemContainerGenerator.PrepareItemContainer(child);
        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return child.DesiredSize;
    }

    /// <summary>
    /// Gets item row index.
    /// </summary>
    private int GetRowIndex(double location)
    {
        int calculatedRowIndex = (int)Math.Floor(location / GetHeight(ChildSize));
        int maxRowIndex = (int)Math.Ceiling(Items.Count / (double)ItemsPerRowCount);

        return Math.Max(Math.Min(calculatedRowIndex, maxRowIndex), 0);
    }

    /// <summary>
    /// Gets container style of the <see cref="ItemsControl"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="property"></param>
    /// <param name="fallbackValue"></param>
    /// <returns></returns>
    private T ReadItemContainerStyle<T>(DependencyProperty property, T fallbackValue) where T : notnull
    {
        object value = ItemsControl
            .ItemContainerStyle
            ?.Setters
            .OfType<Setter>()
        .FirstOrDefault(setter => setter.Property == property)
            ?.Value;
        return (T)(value ?? fallbackValue);
    }

    /// <summary>
    /// Updates child size of <see cref="ItemSize"/>.
    /// </summary>
    private void UpdateChildSize(Size availableSize)
    {
        if (ItemsOwner is IHierarchicalVirtualizationAndScrollInfo groupItem && GetIsVirtualizingWhenGrouping(ItemsControl))
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

        ChildSize = ItemSize != Size.Empty ? ItemSize : InternalChildren.Count != 0 ? InternalChildren[0].DesiredSize : CalculateChildSize(availableSize);

        ItemsPerRowCount = double.IsInfinity(GetWidth(availableSize)) ? Items.Count : Math.Max(1, (int)Math.Floor(GetWidth(availableSize) / GetWidth(ChildSize)));

        RowCount = (int)Math.Ceiling((double)Items.Count / ItemsPerRowCount);
    }
}