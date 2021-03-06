﻿using System;
using System.Linq;
using AsciiConsoleUi;
using AsciiUml.Geo;

namespace AsciiUml.UI {
	public static class PaintServiceCore {
		public static Canvass Paint(State state) {
			var canvas = PaintModel(state.Model, state.PaintSelectableIds);
			canvas = PaintCursor(canvas, state.TheCurser);
			return canvas;
		}

		private static Canvass PaintCursor(Canvass canvas, Cursor cursor) {
			var pixel = canvas.Catode[cursor.Y][cursor.X] ?? (canvas.Catode[cursor.Y][cursor.X] = new Pixel());
			pixel.BackGroundColor = ConsoleColor.DarkYellow;
			pixel.ForegroundColor = ConsoleColor.Yellow;
			return canvas;
		}

		public static Canvass PaintModel(Model model, bool paintSelectableIds) {
			var c = new Canvass();

			foreach (var x in model.Objects) {
				if (x is Database)
					PaintDatabase(c, x as Database, paintSelectableIds);
				if (x is Box)
					PaintBox(c, x as Box, paintSelectableIds);
				if (x is UmlUser)
					PaintUmlUser(c, x as UmlUser, paintSelectableIds);
			}

			// draw lines after boxes so the shortest path does not intersect those objects
			foreach (var x in model.Objects)
				if (x is Line)
					PaintLine2(c, x as Line, model);

			// labels may go above lines
			foreach (var x in model.Objects) {
				if (x is Label)
					PaintLabel(c, x as Label, paintSelectableIds);
				if (x is Note)
					PaintNote(c, x as Note, paintSelectableIds);
			}

			// lines may not cross boxes, hence drawn afterwards
			model.Objects.OfType<SlopedLineVectorized>().Each(x => PaintSlopedLine(c, x, model));
			model.Objects.OfType<SlopedLine2>().Each(x => PaintSlopedLine2(c, x, model));

			return c;
		}

		private static void PaintUmlUser(Canvass canvass, UmlUser user, bool paintSelectableIds) {
			var gfx = @"
,-.
`-'
/|\
 |
/ \
" + (user.Text ?? "");

			var lines = gfx.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
			lines.Each((line, row) => Canvass.PaintString(canvass, line, user.Pos.X, user.Pos.Y + row, user.Id));

			if (paintSelectableIds)
				canvass.RawPaintString(user.Id.ToString(), user.Pos, ConsoleColor.DarkGreen, ConsoleColor.Green);
		}

		//+~~~~~~~~~~+\
		//|          |_\
		//|             |
		//|             |
		//+~~~~~~~~~~~~~+
		private static void PaintNote(Canvass canvass, Note note, bool paintSelectableIds) {
			int px = note.Pos.X, py = note.Pos.Y;
			var rows = note.Text.Split('\n');

			for (var y = 0; y < note.H; y++)
				if (y == 0) {
					var line = "~".Repeat(note.W - 3);
					Canvass.PaintString(canvass, $"+{line}+\\", px, py + y, note.Id, ConsoleColor.Black, ConsoleColor.Gray);
				}
				else if (y == 1) {
					var line = "".PadLeft(note.W - 3);
					Canvass.PaintString(canvass, $"|{line}|_\\", px, py + y, note.Id, ConsoleColor.Black, ConsoleColor.Gray);
				}
				else if (y < note.H - 1) {
					var line = rows[y - 2].PadRight(note.W - 1);
					Canvass.PaintString(canvass, $"|{line}|", px, py + y, note.Id, ConsoleColor.Black, ConsoleColor.Gray);
				}
				else {
					var line = "~".Repeat(note.W - 1);
					Canvass.PaintString(canvass, $"+{line}+", px, py + y, note.Id, ConsoleColor.Black, ConsoleColor.Gray);
				}

			if (paintSelectableIds)
				canvass.RawPaintString(note.Id.ToString(), note.Pos, ConsoleColor.DarkGreen, ConsoleColor.Green);
		}

		private static void PaintSlopedLine2(Canvass canvass, SlopedLine2 slopedLine2, Model model) {
			slopedLine2.Segments.Each((segment, i) => {
				var c = GetLineChar(slopedLine2.GetDirectionOf(i), segment.Type);
				PaintLineOrCross(canvass, segment.Pos, c, slopedLine2.Id, model);
			});
		}

		private static char GetLineChar(LineDirection direction, SegmentType segmentType) {
			switch (segmentType) {
				case SegmentType.Line:
					switch (direction) {
						case LineDirection.East:
						case LineDirection.West:
							return '-';
						case LineDirection.North:
						case LineDirection.South:
							return '|';
						default:
							throw new ArgumentOutOfRangeException();
					}
				case SegmentType.Slope:
					return '+';
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static void PaintSlopedLine(Canvass canvass, SlopedLineVectorized slopedLineVectorized, Model model) {
			foreach (var segment in slopedLineVectorized.Segments) {
				var c = GetLineChar(segment.Direction, segment.Type);

				var delta = Math.Abs(segment.From.X - segment.To.X);
				int direction;
				direction = segment.From.X < segment.To.X ? 1 : -1;
				for (var i = 0; i <= delta; i++) {
					var newPos = new Coord(segment.From.X + i * direction, segment.From.Y);
					PaintLineOrCross(canvass, newPos, c, segment.Id, model);
				}

				direction = segment.From.Y < segment.To.Y ? 1 : -1;
				delta = Math.Abs(segment.From.Y - segment.To.Y);
				for (var i = 0; i <= delta; i++) {
					var newPos = new Coord(segment.From.X, segment.From.Y + i * direction);
					PaintLineOrCross(canvass, newPos, c, segment.Id, model);
				}
			}
		}

		private static void PaintLineOrCross(Canvass canvass, Coord pos, char c, int id, Model model) {
			var oc = canvass.Occupants[pos.Y, pos.X];
			if (oc.HasValue) {
				var elem = model.Objects.First(x => x.Id == oc.Value);
				if (elem is Line || elem is SlopedLineVectorized || elem is SlopedLine2) {
					var cell = canvass.GetCell(pos);
					if (cell == '-' && c == '|' || cell == '|' && c == '-')
						c = '+';
				}
			}

			canvass.Paint(pos, c, id);
		}

		private static void PaintLabel(Canvass canvass, Label label, bool paintSelectableIds) {
			var lines = label.Text.Split('\n');

			switch (label.Direction) {
				case LabelDirection.LeftToRight:
					lines.Each((line, extraY) => Canvass.PaintString(canvass, line, label.X, label.Y + extraY, label.Id, ConsoleColor.Black,
						ConsoleColor.Gray));
					break;

				case LabelDirection.TopDown:
					var extraX = 0;
					foreach (var line in lines) {
						for (var i = 0; i < line.Length; i++)
							canvass.Paint(new Coord(label.X + extraX, label.Y + i), line[i], label.Id);
						extraX++;
					}
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			if (paintSelectableIds)
				canvass.RawPaintString(label.Id.ToString(), label.Pos, ConsoleColor.DarkGreen, ConsoleColor.Green);
		}

		private static char GetCharForStyle(BoxStyle style, BoxFramePart part) {
			switch (style) {
				case BoxStyle.Stars:
					return '*';
				case BoxStyle.Dots:
					return '.';
				case BoxStyle.Eqls:
					return '=';
				case BoxStyle.Lines:
					switch (part) {
						case BoxFramePart.NWCorner:
						case BoxFramePart.NECorner:
						case BoxFramePart.SWCorner:
						case BoxFramePart.SECorner:
							return '+';
						case BoxFramePart.Horizontal:
							return '-';
						case BoxFramePart.Vertical:
							return '|';
						default:
							throw new ArgumentOutOfRangeException(nameof(part));
					}
				default:
					throw new ArgumentOutOfRangeException(nameof(style));
			}
		}

		public static void PaintBox(Canvass c, Box b, bool paintSelectableIds) {
			b.GetFrameParts().Each(part => c.Paint(part.Item1, GetCharForStyle(b.Style, part.Item2), b.Id));
			const int padX = 2, padY = 1; // TODO make padding configurable pr. box
			if (!string.IsNullOrWhiteSpace(b.Text))
				b.Text.Split('\n').Each((text, i) =>
					Canvass.PaintString(c, text, b.X + padX, b.Y + padY + i, b.Id, ConsoleColor.Black, ConsoleColor.Gray));

			if (paintSelectableIds) c.RawPaintString(b.Id.ToString(), b.Pos, ConsoleColor.DarkGreen, ConsoleColor.Green);
		}

		public static void PaintDatabase(Canvass c, Database d, bool statePaintSelectableIds) {
			foreach (var t in d.Paint()) c.Paint(t.Item1, t.Item2, t.Item3);

			if (statePaintSelectableIds) c.RawPaintString(d.Id.ToString(), d.Pos, ConsoleColor.DarkGreen, ConsoleColor.Green);
		}

		public static char CalculateDirectionLine(Coord previous, Coord point, Coord next) {
			if (previous.X == point.X) return point.X == next.X ? '|' : '+';

			if (previous.Y == point.Y) return point.Y == next.Y ? '-' : '+';

			if (previous.X < point.X && previous.Y < point.Y) return '\\';
			if (previous.X < point.X && previous.Y > point.Y) return '/';
			if (previous.X > point.X && previous.Y < point.Y) return '/';
			if (previous.X > point.X && previous.Y > point.Y) return '\\';

			throw new ArgumentException("Cannot find a direction");
		}

		public static char CalculateDirectionArrowHead(Coord previous, Coord point) {
			if (previous.X == point.X) return previous.Y > point.Y ? '^' : 'v';
			if (previous.Y == point.Y) return previous.X > point.X ? '<' : '>';

			// diagonal lines
			return previous.X < point.X ? '>' : '<';
		}

		public static void PaintLine2(Canvass c, Line lineArg, Model model) {
			var from = (IConnectable) model.Objects.First(x => x.Id == lineArg.FromId);
			var to = (IConnectable) model.Objects.First(x => x.Id == lineArg.ToId);
			var smallestDist = CalcSmallestDist(from.GetFrameCoords(), to.GetFrameCoords());

			var line = ShortestPathFinder.Calculate(smallestDist.Min, smallestDist.Max, c, model);
			if (line.Count < 2) return;

			Coord coord;

			// dont draw first nor 2-last elements. First/last elements are box-frames
			var i = 1;
			for (; i < line.Count - 2; i++) {
				coord = line[i];
				var lineChar = CalculateDirectionLine(line[i - 1], coord, line[i + 1]);
				c.Paint(coord, lineChar, lineArg.Id);
			}

			// secondlast element is the arrow head
			coord = line[i];
			c.Paint(coord, CalculateDirectionArrowHead(line[i - 1], coord), lineArg.Id);
		}

		public static Coord[] CalculateBoxOutline(Box b) {
			return RectangleHelper.GetFrameCoords(b.X - 1, b.Y - 1, b.H + 2, b.W + 2);
		}

		public static int ManhattenDistance(Coord a, Coord b) {
			var dist = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
			return dist;
		}

		public static Range<Coord> CalcSmallestDist(Coord[] froms, Coord[] tos) {
			double smallestDist = int.MaxValue;
			Range<Coord> minDist = null;
			foreach (var pointFrom in froms)
			foreach (var pointTo in tos) {
				var dist = ManhattenDistance(pointFrom, pointTo);
				if (dist < smallestDist) {
					smallestDist = dist;
					minDist = new Range<Coord>(pointFrom, pointTo);
				}
			}

			if (minDist == null)
				throw new ArgumentException("no minimum distance");

			return minDist;
		}
	}
}