import ChangeHistoryIcon from '@mui/icons-material/ChangeHistory';
import CircleIcon from '@mui/icons-material/Circle';
import DiamondIcon from '@mui/icons-material/Diamond';
import HexagonIcon from '@mui/icons-material/Hexagon';
import SquareIcon from '@mui/icons-material/Square';
import StarIcon from '@mui/icons-material/Star';

export interface ChoiceShapeProps {
  index: number;
  fontSize?: number;
}

export function ChoiceShape({ index, fontSize = 20 }: ChoiceShapeProps) {
  const sx = { fontSize };
  switch (index % 6) {
    case 0:
      return <ChangeHistoryIcon sx={sx} />;
    case 1:
      return <DiamondIcon sx={sx} />;
    case 2:
      return <CircleIcon sx={sx} />;
    case 3:
      return <SquareIcon sx={sx} />;
    case 4:
      return <StarIcon sx={sx} />;
    default:
      return <HexagonIcon sx={sx} />;
  }
}

export default ChoiceShape;
