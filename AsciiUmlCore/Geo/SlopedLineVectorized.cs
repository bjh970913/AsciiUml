using System;
using System.Collections.Generic;
using System.Linq;
using AsciiConsoleUi;
using LanguageExt;

namespace AsciiUml.Geo {
	public class SlopedLineVectorized : IPaintable<SlopedLineVectorized> {
		public readonly List<LineSegment> Segments = new List<LineSegment>();

		public SlopedLineVectorized() {
			Id = PaintAbles.GlobalId++;
		}

		public SlopedLineVectorized(int id, List<LineSegment> segments) {
			Id = id;
			Segments = segments;
		}

		public int Id { get; }

		public SlopedLineVectorized Move(Coord delta) {
			throw new NotImplementedException();
		}

		public SlopedLineVectorized Move(int x, int y) {
			throw new NotImplementedException();
		}

		public SlopedLineVectorized AutoRoute() {
			return null; // return a new shortest path from start to end
		}

		public SlopedLineVectorized Drag(Coord dragFrom, Coord dragTo) {
			return DragAnArrowLinePiece(dragFrom, dragTo).MatchUnsafe(x => x, () => this);
		}

		public Option<SlopedLineVectorized> DragAnArrowLinePiece(Coord dragFrom, Coord dragTo) {
			var endpoints = MatchEndpoint(dragFrom).ToList();
			if (endpoints.Any()) {
				var newList = Segments.ToList();
				var match = endpoints.First();
				var deltaX = dragTo.X - dragFrom.X;
				var deltaY = dragTo.Y - dragFrom.Y;
				var delta = new Coord(deltaX, deltaY);

				var currentSegment = newList[match.Item1];

				if (currentSegment.Type == SegmentType.Slope) {
					newList.Insert(0, new LineSegment(this, dragTo, dragTo, SegmentType.Line));
				}
				else if (IsLineAtomic()) {
					//
					newList[match.Item1] = newList[match.Item1].ExtendEndpoint(deltaX, deltaY, match.Item2);
				}
				else if (IsDragDiagonalOfLine(currentSegment, dragFrom, dragTo)) {
					if (newList[match.Item1].IsReducable())
						newList[match.Item1] = currentSegment.Reduce(match.Item2);
					else
						newList.RemoveAt(match.Item1);

					var slopePoint = match.Item2 == EndpointKind.From
						? currentSegment.From
						: currentSegment.To;
					newList.Insert(match.Item1, new LineSegment(this, slopePoint, slopePoint, SegmentType.Slope));

					var directionFromBend = LineDirections.GetDirectionFromBend(currentSegment.Direction, match.Item2, deltaX, deltaY);
					var newSegmentPos = match.Item2 == EndpointKind.From
						? currentSegment.From.Move(delta)
						: currentSegment.To.Move(delta);
					newList.Insert(match.Item1,
						new LineSegment(this, newSegmentPos, newSegmentPos, SegmentType.Line, directionFromBend));
				}
				else {
					if (currentSegment.SpanOneCell())
						if (Vector.IsDirectionOpposite(currentSegment.Direction, Vector.GetDirection(dragFrom, dragTo)))
							newList.RemoveAt(match.Item1);
						else
							newList[match.Item1] = newList[match.Item1].ExtendEndpoint(deltaX, deltaY, match.Item2);
					else
						for (var i = match.Item1; i < newList.Count; i++) {
							if (newList[i].Type == SegmentType.Slope)
								break;
							newList[i] = newList[i].ExtendEndpoint(deltaX, deltaY, match.Item2);
						}
				}
				return new SlopedLineVectorized(Id, newList);
			}

			var pos = -1;
			var matchedSegment = Segments.FirstOrDefault(s => IsPointPartOfLine(s.From, s.To, dragFrom), p => pos = p);
			var noLinesAreHit = matchedSegment == null;
			if (noLinesAreHit)
				return null;

			var isMoveWithinLine = IsPointPartOfLine(matchedSegment.From, matchedSegment.To, dragTo);
			if (isMoveWithinLine)
				return null;
			return null;
		}

		private bool IsLineAtomic() {
			return Segments.Count == 1 && Segments[0].From == Segments[0].To;
		}

		private bool IsDragDiagonalOfLine(LineSegment segment, Coord dragFrom, Coord dragTo) {
			if (segment.Direction == LineDirection.East || segment.Direction == LineDirection.West)
				return dragFrom.Y != dragTo.Y;

			return dragFrom.X != dragTo.X;
		}


		private IEnumerable<Tuple<int, EndpointKind>> MatchEndpoint(Coord dragFrom) {
			for (var i = 0; i < Segments.Count; i++) {
				var segment = Segments[i];

				if (segment.From == segment.To) {
					if (segment.To == dragFrom) yield return Tuple.Create(i, EndpointKind.To);
				}
				else {
					if (segment.From == dragFrom) yield return Tuple.Create(i, EndpointKind.From);
					if (segment.To == dragFrom && segment.From != segment.To) yield return Tuple.Create(i, EndpointKind.To);
				}
			}
		}

		private bool IsPointPartOfLine(Coord lineFrom, Coord lineTo, Coord point) {
			if (lineFrom.X <= point.X && point.X <= lineTo.X && lineFrom.Y <= point.Y && point.Y <= lineTo.Y) return true;
			return false;
		}
	}

	public class LineSegment {
		public readonly LineDirection Direction;
		public readonly Coord From, To;
		public readonly SlopedLineVectorized Origin;
		public readonly SegmentType Type;

		public LineSegment(SlopedLineVectorized l, Coord from, Coord to, SegmentType type)
			: this(PaintAbles.GlobalId++, l, from, to, type, Vector.GetDirection(from, to)) {
		}

		public LineSegment(SlopedLineVectorized l, Coord from, Coord to, SegmentType type, LineDirection direction)
			: this(PaintAbles.GlobalId++, l, from, to, type, direction) {
		}

		public LineSegment(int id, SlopedLineVectorized l, Coord from, Coord to, SegmentType type)
			: this(id, l, from, to, type, Vector.GetDirection(from, to)) {
		}

		public LineSegment(int id, SlopedLineVectorized l, Coord from, Coord to, SegmentType type, LineDirection direction) {
			if (type == SegmentType.Slope && from != to)
				throw new ArgumentException("Slopes can only be size 1");

			Id = id;
			Origin = l;
			From = from;
			To = to;
			Type = type;
			Direction = direction;
		}

		public int Id { get; }

		public LineSegment ExtendEndpoint(int x, int y, EndpointKind kind) {
			Coord newTo, newFrom;

			switch (kind) {
				case EndpointKind.From:
					newFrom = new Coord(From.X + x, From.Y + y);
					newTo = To;
					break;
				case EndpointKind.To:
					newFrom = From;
					newTo = new Coord(To.X + x, To.Y + y);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
			}
			return new LineSegment(Id, Origin, newFrom, newTo, Type);
		}

		public LineSegment Reduce(EndpointKind kind) {
			if (!IsReducable())
				throw new ArgumentException("cannot reduce line of length 1");

			switch (kind) {
				case EndpointKind.From:
					switch (Direction) {
						case LineDirection.North:
							return new LineSegment(Id, Origin, From.Move(Vector.DeltaNorth), To, Type, Direction);
						case LineDirection.South:
							return new LineSegment(Id, Origin, From.Move(Vector.DeltaSouth), To, Type, Direction);
						case LineDirection.East:
							return new LineSegment(Id, Origin, From.Move(Vector.DeltaEast), To, Type, Direction);
						case LineDirection.West:
							return new LineSegment(Id, Origin, From.Move(Vector.DeltaWest), To, Type, Direction);
						default:
							throw new ArgumentOutOfRangeException();
					}
				case EndpointKind.To:
					switch (Direction) {
						case LineDirection.North:
							return new LineSegment(Id, Origin, From, To.Move(Vector.DeltaSouth), Type, Direction);
						case LineDirection.South:
							return new LineSegment(Id, Origin, From, To.Move(Vector.DeltaNorth), Type, Direction);
						case LineDirection.East:
							return new LineSegment(Id, Origin, From, To.Move(Vector.DeltaWest), Type, Direction);
						case LineDirection.West:
							return new LineSegment(Id, Origin, From, To.Move(Vector.DeltaEast), Type, Direction);
						default:
							throw new ArgumentOutOfRangeException();
					}
				default:
					throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
			}
		}

		public bool IsReducable() {
			return From != To;
		}

		public bool SpanOneCell() {
			return From == To;
		}
	}
}