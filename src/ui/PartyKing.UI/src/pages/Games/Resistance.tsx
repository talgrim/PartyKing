import { ReactNode, useState } from 'react';
import { useParams } from 'react-router-dom';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { PageNavigation } from '@/components/common/PageNavigation';
import { MissionBoardIcon } from '@/pages/Games/resistance/ResistanceIcons';
import { RESISTANCE_RULES } from '@/pages/Games/resistance/resistanceRules';
import { sessionGamesPath, sessionPath } from '@/router/constants';
import { useViewerRole } from '@/service/endpoints/sessions/useViewerRole';

// Mocked until the game is stored per session: a table for 5 players with nothing played yet
const TEAM_SIZES = [2, 3, 2, 3, 3];
const NO_RESULTS = [null, null, null, null, null] as const;

const navClass = 'px-3 py-2 text-slate-200 bg-slate-900 rounded-md';

/**
 * The Resistance page: the mission board and the rules summary behind the "i" button. The admin's "Open the game"
 * button is disabled for now, there is no game state on the server yet.
 */
export const Resistance = (): ReactNode => {
  const { sessionId = '' } = useParams();
  const { isHost, isAdmin } = useViewerRole(sessionId);
  const [rulesOpen, setRulesOpen] = useState(false);

  const back = isHost || isAdmin
    ? { to: sessionGamesPath(sessionId), label: 'Back to the games' }
    : { to: sessionPath(sessionId), label: 'Back to the session' };

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="w-full max-w-3xl mx-auto px-2 pt-4">
        <div className="flex items-center gap-2">
          <PageNavigation links={[back]} />
          {isAdmin && (
            <button type="button" disabled className={`${navClass} opacity-50`}>
              Open the game
            </button>
          )}
          <button
            type="button"
            onClick={() => setRulesOpen(true)}
            aria-label="Rules"
            title="Rules"
            className="ml-auto p-1 text-slate-400 hover:text-slate-200 cursor-pointer"
          >
            <InfoOutlinedIcon />
          </button>
        </div>

        <MissionBoardIcon
          className="w-full mt-6"
          title="Mission board"
          teamSizes={TEAM_SIZES}
          results={NO_RESULTS}
          currentMission={null}
          rejectedTeams={0}
          fourthMissionNeedsTwoFails={false}
        />
      </div>

      <Dialog
        open={rulesOpen}
        onClose={() => setRulesOpen(false)}
        aria-labelledby="resistance-rules-title"
        slotProps={{ paper: { className: 'bg-slate-800! text-slate-200!' } }}
      >
        <DialogTitle id="resistance-rules-title">Zasady gry Resistance</DialogTitle>
        <DialogContent className="space-y-3">
          {RESISTANCE_RULES.map((rule) => (
            <section key={rule.title}>
              <h2 className="text-slate-200">{rule.title}</h2>
              <p className="text-sm text-slate-400">{rule.text}</p>
            </section>
          ))}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRulesOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </div>
  );
};
