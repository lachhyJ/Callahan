import { Link } from 'react-router-dom'
import { activityLabel } from '../utils/activityLabel'
import { formatDateMedium } from '../dateUtils'

// An Ultimate activity attached to a tournament but not tagged "Game": the
// label links to the activity, and a separate button tags it in one tap so a
// stray tap on the row can't classify anything.
export default function PendingGameRow({ activity, onMarkGame, disabled }) {
  return (
    <div className="history-item pending-game-row">
      <Link to={`/activities/${activity.id}`} className="history-item games-list-row">
        <span className="history-item-main">
          {formatDateMedium(activity.date)} · {activityLabel(activity)}
        </span>
      </Link>
      <button
        type="button"
        className="secondary-btn pending-game-btn"
        onClick={() => onMarkGame(activity)}
        disabled={disabled}
      >
        Mark as game
      </button>
    </div>
  )
}
