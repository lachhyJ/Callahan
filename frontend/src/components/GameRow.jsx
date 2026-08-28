import { Link } from 'react-router-dom'
import { activityLabel, livePlayTeaser } from '../utils/activityLabel'
import { formatDateMedium } from '../dateUtils'

// A single Ultimate "Game" row linking through to its detail page, with the
// one-glance live-play teaser when the metrics exist. Shared by the games
// list and the tournament detail page's game-by-game section.
export default function GameRow({ game }) {
  const teaser = livePlayTeaser(game)
  return (
    <Link to={`/activities/${game.id}`} className="history-item games-list-row">
      <div className="history-item-row">
        <span className="history-item-main">
          {formatDateMedium(game.date)} · {activityLabel(game)}
          {teaser && <span className="activity-classify-teaser"> · {teaser}</span>}
        </span>
      </div>
    </Link>
  )
}
