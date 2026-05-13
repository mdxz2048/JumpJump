const slotLabels = {
  Top: "上方",
  TopRight: "右上",
  BottomRight: "右下",
  Bottom: "下方",
  BottomLeft: "左下",
  TopLeft: "左上"
};

const pieceColors = {
  Top: "rgb(231, 185, 151)",
  TopRight: "rgb(150, 187, 247)",
  BottomRight: "rgb(211, 237, 140)",
  Bottom: "rgb(239, 165, 191)",
  BottomLeft: "rgb(183, 152, 247)",
  TopLeft: "rgb(171, 201, 191)"
};

const unityColors = {
  boardCell: "rgba(219, 237, 250, .86)",
  boardTarget: "rgba(247, 255, 255, .98)",
  slotRing: "rgba(143, 158, 168, .72)",
  targetRing: "rgba(82, 168, 255, .92)",
  targetDot: "rgba(255, 122, 13, .82)",
  selection: "rgba(255, 255, 255, .72)",
  shadow: "rgba(10, 15, 20, .3)",
  gameChrome: "rgba(0, 0, 0, .48)"
};

const unityBoard = {
  safeMarginRatio: 0.015,
  baseCellRadius: 61,
  baseSlotRingSize: 70,
  basePieceShadowSize: 96,
  basePieceSize: 94,
  baseSelectionSize: 82
};

const slotCampAnchors = {
  Top: { q: 4, r: -8 },
  TopRight: { q: 8, r: -4 },
  BottomRight: { q: 4, r: 4 },
  Bottom: { q: -4, r: 8 },
  BottomLeft: { q: -8, r: 4 },
  TopLeft: { q: -4, r: -4 }
};

const state = {
  ws: null,
  authed: false,
  adminAuthed: false,
  adminTapCount: 0,
  account: "",
  displayName: "",
  roomKey: "",
  mySlot: "",
  isHost: false,
  snapshot: null,
  seats: [],
  selectedPieceId: -1,
  cellPoints: new Map(),
  createCooldownUntil: 0,
  createCooldownTimer: 0,
  piecePulses: new Map(),
  renderLoopId: null
};

const els = {
  loginView: document.getElementById("loginView"),
  adminView: document.getElementById("adminView"),
  lobbyView: document.getElementById("lobbyView"),
  gameView: document.getElementById("gameView"),
  lobbyPanel: document.getElementById("lobbyPanel"),
  roomPanel: document.getElementById("roomPanel"),
  accountInput: document.getElementById("accountInput"),
  passwordInput: document.getElementById("passwordInput"),
  adminPanel: document.getElementById("adminPanel"),
  adminPasswordInput: document.getElementById("adminPasswordInput"),
  loginButton: document.getElementById("loginButton"),
  adminLoginButton: document.getElementById("adminLoginButton"),
  adminRefreshButton: document.getElementById("adminRefreshButton"),
  adminBackButton: document.getElementById("adminBackButton"),
  newAccountInput: document.getElementById("newAccountInput"),
  newPasswordInput: document.getElementById("newPasswordInput"),
  newNameInput: document.getElementById("newNameInput"),
  addPlayerButton: document.getElementById("addPlayerButton"),
  adminAccounts: document.getElementById("adminAccounts"),
  nicknameInput: document.getElementById("nicknameInput"),
  saveNicknameButton: document.getElementById("saveNicknameButton"),
  createButton: document.getElementById("createButton"),
  joinButton: document.getElementById("joinButton"),
  refreshButton: document.getElementById("refreshButton"),
  startButton: document.getElementById("startButton"),
  finishButton: document.getElementById("finishButton"),
  passButton: document.getElementById("passButton"),
  roomKeyInput: document.getElementById("roomKeyInput"),
  ruleSelect: document.getElementById("ruleSelect"),
  roomList: document.getElementById("roomList"),
  adminMembers: document.getElementById("adminMembers"),
  adminRooms: document.getElementById("adminRooms"),
  roomKeyText: document.getElementById("roomKeyText"),
  slotText: document.getElementById("slotText"),
  seatList: document.getElementById("seatList"),
  statusText: document.getElementById("statusText"),
  loginStatusText: document.getElementById("loginStatusText"),
  mobileStatusText: document.getElementById("mobileStatusText"),
  currentTurnPiece: document.getElementById("currentTurnPiece"),
  currentTurnText: document.getElementById("currentTurnText"),
  gameTitle: document.getElementById("gameTitle"),
  canvas: document.getElementById("boardCanvas")
};

const ctx = els.canvas.getContext("2d");

function connect() {
  const protocol = location.protocol === "https:" ? "wss:" : "ws:";
  state.ws = new WebSocket(`${protocol}//${location.host}/ws`);
  state.ws.addEventListener("open", () => setStatus("已连接服务器，请登录。"));
  state.ws.addEventListener("message", event => handleMessage(JSON.parse(event.data)));
  state.ws.addEventListener("close", () => {
    setStatus("连接已断开。");
    state.authed = false;
    state.roomKey = "";
    state.snapshot = null;
    refreshPanels();
  });
}

function send(message) {
  if (!state.ws || state.ws.readyState !== WebSocket.OPEN) {
    setStatus("服务器未连接。");
    return;
  }

  state.ws.send(JSON.stringify(message));
}

function handleMessage(message) {
  switch (message.type) {
    case "WELCOME":
      setStatus("服务器已就绪。");
      break;
    case "AUTH_OK":
      state.authed = true;
      state.account = message.account || els.accountInput.value.trim();
      state.displayName = message.name || state.account;
      els.nicknameInput.value = state.displayName;
      localStorage.setItem("sweetJumpJumpAccount", state.account);
      setStatus(message.message || "进入成功。");
      refreshPanels();
      break;
    case "ADMIN_OK":
      state.adminAuthed = true;
      setStatus(message.message || "管理员已进入。");
      refreshPanels();
      send({ type: "ADMIN_SNAPSHOT" });
      break;
    case "ADMIN_SNAPSHOT":
      renderAdminSnapshot(message.members || [], message.rooms || [], message.accounts || []);
      break;
    case "ADMIN_NOTICE":
      setStatus(message.message || "管理员操作完成。");
      break;
    case "PROFILE":
      state.displayName = message.name || state.displayName;
      els.nicknameInput.value = state.displayName;
      setStatus(message.message || "昵称已更新。");
      break;
    case "ERROR":
      setStatus(message.message || "操作失败。");
      break;
    case "ROOM_LIST":
      renderRoomList(message.rooms || []);
      break;
    case "ROOM":
      state.roomKey = message.roomKey;
      state.mySlot = message.slot;
      state.isHost = !!message.isHost;
      state.snapshot = null;
      state.seats = [];
      setStatus(message.message);
      refreshPanels();
      renderRoomShell();
      break;
    case "LOBBY":
      if (message.room) {
        state.seats = message.room.players || [];
        updateHostFlagFromSeats();
        renderSeats();
      }
      break;
    case "STATE": {
      const prevPieces = state.snapshot ? (state.snapshot.pieces || []) : [];
      state.roomKey = message.roomKey || state.roomKey;
      state.snapshot = message.snapshot;
      state.seats = message.seats || state.seats;
      state.isHost = !!message.isHost;
      state.selectedPieceId = state.snapshot ? state.snapshot.selectedPieceId : -1;
      // Detect piece movements and trigger arrival-flash animations
      if (state.snapshot) {
        for (const piece of state.snapshot.pieces || []) {
          const prev = prevPieces.find(p => p.pieceId === piece.pieceId);
          if (prev && (prev.position.q !== piece.position.q || prev.position.r !== piece.position.r)) {
            state.piecePulses.set(coordKey(piece.position), { startTime: performance.now(), maxScale: 1.18, duration: 180 });
          }
        }
      }
      setStatus(state.snapshot ? state.snapshot.statusMessage : "");
      renderRoomShell();
      renderSeats();
      renderCurrentTurn();
      drawBoard();
      refreshActions();
      refreshPanels();
      break;
    }
  }
}

function refreshPanels() {
  els.loginView.classList.toggle("hidden", state.authed || state.adminAuthed);
  els.adminView.classList.toggle("hidden", !state.adminAuthed);
  els.lobbyView.classList.toggle("hidden", !state.authed || !!state.snapshot);
  els.gameView.classList.toggle("hidden", !state.authed || !state.snapshot);
  els.roomPanel.classList.toggle("hidden", !state.roomKey);
  els.startButton.disabled = !state.isHost || !!state.snapshot;
  refreshActions();
  refreshCreateButton();

  if (state.authed && !!state.snapshot) {
    startRenderLoop();
  } else {
    stopRenderLoop();
  }
}

function renderRoomShell() {
  els.roomKeyText.textContent = state.roomKey || "----";
  els.slotText.textContent = state.mySlot ? slotLabels[state.mySlot] : "-";
  els.gameTitle.textContent = state.roomKey ? `在线房间 ${state.roomKey}` : "在线跳跳棋";
}

function renderCurrentTurn() {
  const snapshot = state.snapshot;
  if (!snapshot) {
    els.currentTurnText.textContent = "等待开局";
    els.currentTurnPiece.style.background = pieceBackground(pieceColors.Bottom);
    return;
  }

  const slot = snapshot.currentPlayerSlot;
  const name = getPlayerName(slot, snapshot.currentPlayerKind);
  els.currentTurnText.textContent = `轮到 ${name} · ${slotLabels[slot] || slot}`;
  els.currentTurnPiece.style.background = pieceBackground(pieceColors[slot] || "rgb(255,255,255)");
}

function renderRoomList(rooms) {
  els.roomList.innerHTML = "";
  if (rooms.length === 0) {
    els.roomList.innerHTML = `<div class="room-card">暂无可加入房间</div>`;
    return;
  }

  for (const room of rooms) {
    const card = document.createElement("div");
    card.className = "room-card";
    const seats = (room.players || []).map(player => `${slotLabels[player.slot] || player.slot}:${player.name}`).join("  ");
    card.innerHTML = `<strong>房间 ${room.roomKey}</strong><br>${room.players.length}/6 人 · ${ruleLabel(room.ruleVariant)}<br>${seats || "等待玩家"}`;
    card.addEventListener("click", () => {
      els.roomKeyInput.value = room.roomKey;
      send({ type: "JOIN", roomKey: room.roomKey });
    });
    els.roomList.appendChild(card);
  }
}

function renderSeats() {
  els.seatList.innerHTML = "";

  if (state.snapshot) {
    // In-game: simple seat list
    for (const seat of state.seats) {
      const div = document.createElement("div");
      div.className = "seat";
      div.textContent = `${slotLabels[seat.slot] || seat.slot}: ${seat.name}${seat.isHost ? " · 房主" : ""}`;
      els.seatList.appendChild(div);
    }
    return;
  }

  // Lobby: interactive slot picker — show all 6 positions
  const allSlots = ["Top", "TopRight", "BottomRight", "Bottom", "BottomLeft", "TopLeft"];
  for (const slot of allSlots) {
    const seat = state.seats.find(s => s.slot === slot);
    const isMe = slot === state.mySlot;
    const taken = !!seat && !isMe;

    const div = document.createElement("div");
    div.className = `slot-pick-item${isMe ? " my-slot" : ""}${taken ? " taken" : ""}`;

    const dot = document.createElement("span");
    dot.className = "slot-dot";
    dot.style.background = pieceColors[slot];
    div.appendChild(dot);

    const labelSpan = document.createElement("span");
    labelSpan.className = "slot-pick-label";
    labelSpan.textContent = slotLabels[slot] || slot;
    div.appendChild(labelSpan);

    const occupantSpan = document.createElement("span");
    occupantSpan.className = "slot-pick-occupant";
    occupantSpan.textContent = seat ? seat.name + (seat.isHost ? " · 房主" : "") : "空位";
    div.appendChild(occupantSpan);

    if (!taken) {
      div.title = isMe ? "当前位置" : "点击选择此位置";
      div.addEventListener("click", () => {
        if (!isMe) send({ type: "SELECT_SLOT", slot });
      });
    }
    els.seatList.appendChild(div);
  }
}

function updateHostFlagFromSeats() {
  const mySeat = state.seats.find(value => value.slot === state.mySlot);
  state.isHost = !!(mySeat && mySeat.isHost);
}

function renderAdminSnapshot(members, rooms, accounts) {
  if (accounts.length === 0) {
    els.adminAccounts.innerHTML = `<div class="room-card">暂无棋手账号</div>`;
  } else {
    els.adminAccounts.innerHTML = accounts.map(account => {
      const status = account.disabled ? " · 已禁用" : (account.online ? " · 在线" : " · 离线");
      const disableBtn = account.disabled
        ? `<button class="admin-action-btn" data-action="enable" data-account="${escapeHtml(account.account)}">启用</button>`
        : `<button class="admin-action-btn" data-action="disable" data-account="${escapeHtml(account.account)}">禁用</button>`;
      const removeBtn = `<button class="admin-action-btn danger" data-action="remove" data-account="${escapeHtml(account.account)}">删除</button>`;
      return `<div class="room-card${account.disabled ? " disabled-account" : ""}"><strong>${escapeHtml(account.account)}</strong><br>${escapeHtml(account.name)}${status}<div class="admin-account-actions">${disableBtn}${removeBtn}</div></div>`;
    }).join("");
    els.adminAccounts.querySelectorAll(".admin-action-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        const action = btn.dataset.action;
        const account = btn.dataset.account;
        if (action === "enable") {
          send({ type: "ENABLE_PLAYER", account });
        } else if (action === "disable") {
          send({ type: "DISABLE_PLAYER", account });
        } else if (action === "remove") {
          if (window.confirm(`确定要删除账号 "${account}" 吗？此操作不可撤销。`)) {
            send({ type: "REMOVE_PLAYER", account });
          }
        }
      });
    });
  }

  els.adminMembers.innerHTML = members.length === 0
    ? `<div class="room-card">暂无在线成员</div>`
    : members.map(member => {
      const slot = member.slot ? ` · ${slotLabels[member.slot] || member.slot}` : "";
      const room = member.roomKey ? `房间 ${member.roomKey}` : "未入房";
      const tags = `${member.isAdmin ? " · 管理员" : ""}${member.isHost ? " · 房主" : ""}`;
      const account = member.account ? `账号 ${escapeHtml(member.account)} · ` : "";
      return `<div class="room-card"><strong>${escapeHtml(member.name || member.clientId)}</strong><br>${account}${room}${slot}${tags}</div>`;
    }).join("");

  els.adminRooms.innerHTML = rooms.length === 0
    ? `<div class="room-card">暂无房间</div>`
    : rooms.map(room => {
      const players = (room.players || []).map(player => `${slotLabels[player.slot] || player.slot}:${escapeHtml(player.name)}${player.isHost ? "(房主)" : ""}`).join("  ");
      return `<div class="room-card"><strong>房间 ${room.roomKey}</strong><br>${room.started ? "已开始" : "等待中"} · ${ruleLabel(room.ruleVariant)}<br>${players || "空房间"}</div>`;
    }).join("");
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[char]));
}

function getPlayerName(slot, kind) {
  const seat = state.seats.find(value => value.slot === slot);
  if (seat && seat.name) {
    return seat.name;
  }

  return kind === "Human" ? "玩家" : "高级人机";
}

function refreshActions() {
  const snapshot = state.snapshot;
  const myTurn = snapshot && snapshot.currentPlayerSlot === state.mySlot && snapshot.currentPlayerKind === "Human" && !snapshot.isGameOver;
  els.finishButton.disabled = !myTurn || !snapshot.hasMovedThisTurn;
  els.passButton.disabled = !myTurn || snapshot.hasMovedThisTurn;
}

function setStatus(text) {
  const value = text || "";
  els.statusText.textContent = value;
  els.loginStatusText.textContent = value;
  els.mobileStatusText.textContent = value || "请选择一个房间开始。";
}

function pieceBackground(color) {
  return `radial-gradient(circle at 34% 32%, rgba(255,255,255,.82) 0 12%, transparent 13%), radial-gradient(circle at 50% 50%, ${color} 0 45%, ${shadeRgb(color, -38)} 100%)`;
}

function ruleLabel(value) {
  return value === "SpaceJump" ? "空跳" : "一子跳";
}

function refreshCreateButton() {
  if (!els.createButton) {
    return;
  }

  const remaining = Math.ceil((state.createCooldownUntil - Date.now()) / 1000);
  if (remaining > 0) {
    els.createButton.disabled = true;
    els.createButton.textContent = `${remaining}s`;
  } else {
    els.createButton.disabled = false;
    els.createButton.textContent = "建房";
    if (state.createCooldownTimer) {
      window.clearInterval(state.createCooldownTimer);
      state.createCooldownTimer = 0;
    }
  }
}

function startCreateCooldown() {
  state.createCooldownUntil = Date.now() + 10000;
  refreshCreateButton();
  if (!state.createCooldownTimer) {
    state.createCooldownTimer = window.setInterval(refreshCreateButton, 250);
  }
}

function getCellRadius() {
  const safeMargin = 0.015;
  const avail = els.canvas.width * (1 - 2 * safeMargin);
  return Math.min(avail / (12 * Math.sqrt(3) + 2), avail / 26);
}

function axialToPixel(coord) {
  const size = getCellRadius();
  const centerX = els.canvas.width / 2;
  const centerY = els.canvas.height / 2;
  const x = size * Math.sqrt(3) * (coord.q + coord.r / 2);
  const y = size * 1.5 * coord.r;  // positive: matches Unity world-space y-up convention via canvas y-down flip
  const rotated = rotatePoint(x, y, getBoardViewRotationDegrees());
  return {
    x: centerX + rotated.x,
    y: centerY + rotated.y
  };
}

function getBoardViewRotationDegrees() {
  switch (state.mySlot) {
    case "Bottom":
      return 0;
    case "BottomLeft":
      return 60;
    case "TopLeft":
      return 120;
    case "Top":
      return 180;
    case "TopRight":
      return -120;
    case "BottomRight":
      return -60;
    default:
      return 0;
  }
}

function rotatePoint(x, y, degrees) {
  const radians = degrees * Math.PI / 180;
  const cos = Math.cos(radians);
  const sin = Math.sin(radians);
  return {
    x: x * cos - y * sin,
    y: x * sin + y * cos
  };
}

// Cache board cells — they never change
const cachedCells = allCells();

// Cache cell pixel positions (invalidated when rotation/radius changes)
let cachedCellPoints = null;
let cachedCellRadius = -1;
let cachedRotation = null;

function getCachedCellPoints() {
  const r = getCellRadius();
  const rot = getBoardViewRotationDegrees();
  if (cachedCellPoints && cachedCellRadius === r && cachedRotation === rot) {
    return cachedCellPoints;
  }
  cachedCellPoints = new Map();
  for (const cell of cachedCells) {
    cachedCellPoints.set(coordKey(cell), axialToPixel(cell));
  }
  cachedCellRadius = r;
  cachedRotation = rot;
  return cachedCellPoints;
}

function drawBoard() {
  const r = getCellRadius();
  ctx.clearRect(0, 0, els.canvas.width, els.canvas.height);
  drawUnityMountainBackdrop();

  const pts = getCachedCellPoints();
  state.cellPoints = pts;

  const legalKeys = new Set((state.snapshot?.legalTargets || []).map(coordKey));
  for (const cell of cachedCells) {
    const point = pts.get(coordKey(cell));
    drawUnityCell(point.x, point.y, r, legalKeys.has(coordKey(cell)));
  }

  if (!state.snapshot) {
    return;
  }

  for (const piece of state.snapshot.pieces || []) {
    const key = coordKey(piece.position);
    const point = pts.get(key) || axialToPixel(piece.position);
    drawUnityPiece(point.x, point.y, r, pieceColors[piece.owner] || "rgb(255,255,255)", piece.pieceId === state.selectedPieceId, key);
  }

  drawPlayerNameLabels();
}

function drawPlayerNameLabels() {
  if (!state.snapshot) {
    return;
  }

  for (const player of state.snapshot.players || []) {
    const slot = player.slotId;
    const anchor = slotCampAnchors[slot];
    if (!anchor) {
      continue;
    }

    const anchorPoint = getCachedCellPoints().get(coordKey(anchor)) || axialToPixel(anchor);
    const center = { x: els.canvas.width / 2, y: els.canvas.height / 2 };
    const dx = anchorPoint.x - center.x;
    const dy = anchorPoint.y - center.y;
    const length = Math.max(1, Math.hypot(dx, dy));
    const labelPoint = {
      x: anchorPoint.x + dx / length * 34,
      y: anchorPoint.y + dy / length * 26
    };
    drawNameLabel(labelPoint.x, labelPoint.y, getPlayerName(slot, player.playerKind), pieceColors[slot] || "rgb(255,255,255)");
  }
}

function drawNameLabel(x, y, name, color) {
  ctx.save();
  ctx.font = "700 22px -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif";
  const paddingX = 14;
  const width = Math.min(180, ctx.measureText(name).width + paddingX * 2);
  const height = 38;
  const left = Math.max(12, Math.min(els.canvas.width - width - 12, x - width / 2));
  const top = Math.max(12, Math.min(els.canvas.height - height - 132, y - height / 2));
  roundRect(left, top, width, height, 8);
  ctx.fillStyle = "rgba(0,0,0,.42)";
  ctx.fill();
  ctx.strokeStyle = color;
  ctx.lineWidth = 2;
  ctx.stroke();
  ctx.fillStyle = "rgba(255,255,255,.94)";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(name, left + width / 2, top + height / 2 + 1, width - paddingX);
  ctx.restore();
}

function roundRect(x, y, width, height, radius) {
  ctx.beginPath();
  ctx.moveTo(x + radius, y);
  ctx.arcTo(x + width, y, x + width, y + height, radius);
  ctx.arcTo(x + width, y + height, x, y + height, radius);
  ctx.arcTo(x, y + height, x, y, radius);
  ctx.arcTo(x, y, x + width, y, radius);
  ctx.closePath();
}

// Offscreen canvas cache for the mountain backdrop (generated once)
let backdropCanvas = null;

function ensureBackdrop() {
  if (backdropCanvas) return;
  backdropCanvas = document.createElement("canvas");
  backdropCanvas.width = els.canvas.width;
  backdropCanvas.height = els.canvas.height;
  const offCtx = backdropCanvas.getContext("2d");
  const image = offCtx.createImageData(backdropCanvas.width, backdropCanvas.height);
  for (let y = 0; y < image.height; y++) {
    const ny = 1 - y / (image.height - 1);
    for (let x = 0; x < image.width; x++) {
      const nx = x / (image.width - 1);
      let color = mix([0.9, 0.96, 1], [1, 1, 1], Math.pow(ny, 0.72));

      const farPeak = mountainHeight(nx, 0.71, 0.085, 1.8, 3.4);
      if (ny < farPeak) {
        const depth = clamp01((farPeak - ny) / 0.36);
        const snow = valueNoise(nx * 9 + 2.1, ny * 13 + 1.7);
        const rock = mix([0.78, 0.86, 0.92], [0.54, 0.62, 0.68], depth * 0.7);
        color = mix(rock, [0.95, 0.985, 1], snow > 0.56 ? 0.56 : 0.24);
      }

      const midPeak = mountainHeight(nx + 0.18, 0.55, 0.11, 2.4, 4.7);
      if (ny < midPeak) {
        const depth = clamp01((midPeak - ny) / 0.34);
        const snow = valueNoise(nx * 12 + 7.3, ny * 17 + 9.2);
        const rock = mix([0.7, 0.78, 0.84], [0.42, 0.49, 0.55], depth * 0.82);
        color = mix(rock, [0.91, 0.97, 1], snow > 0.52 ? 0.62 : 0.2);
      }

      const frontPeak = mountainHeight(nx + 0.43, 0.26, 0.08, 3.2, 5.5);
      if (ny < frontPeak) {
        const depth = clamp01((frontPeak - ny) / 0.28);
        const snow = valueNoise(nx * 18 + 5.8, ny * 20 + 4.1);
        const rock = mix([0.64, 0.72, 0.78], [0.34, 0.41, 0.47], depth * 0.85);
        color = mix(rock, [0.88, 0.96, 1], snow > 0.5 ? 0.52 : 0.18);
      }

      const index = (y * image.width + x) * 4;
      image.data[index] = Math.round(color[0] * 255);
      image.data[index + 1] = Math.round(color[1] * 255);
      image.data[index + 2] = Math.round(color[2] * 255);
      image.data[index + 3] = 255;
    }
  }
  offCtx.putImageData(image, 0, 0);
}

function drawUnityMountainBackdrop() {
  ensureBackdrop();
  ctx.drawImage(backdropCanvas, 0, 0);
}

function mountainHeight(x, baseHeight, amplitude, frequencyA, frequencyB) {
  const ridge = Math.sin(x * Math.PI * frequencyA) * 0.52 + Math.sin((x + 0.17) * Math.PI * frequencyB) * 0.28;
  const noise = valueNoise(x * 4.2, 0.37) * 0.42;
  return baseHeight + amplitude * (ridge + noise);
}

function drawUnityCell(x, y, radius, legal) {
  ctx.beginPath();
  for (let i = 0; i < 6; i++) {
    const angle = Math.PI / 180 * (60 * i - 30);
    const px = x + radius * Math.cos(angle);
    const py = y + radius * Math.sin(angle);
    if (i === 0) {
      ctx.moveTo(px, py);
    } else {
      ctx.lineTo(px, py);
    }
  }
  ctx.closePath();
  ctx.fillStyle = legal ? unityColors.boardTarget : unityColors.boardCell;
  ctx.fill();
  ctx.lineWidth = 2;
  ctx.strokeStyle = legal ? "rgba(255,255,255,.62)" : "rgba(148,158,168,.58)";
  ctx.stroke();

  const ringRadius = radius * (unityBoard.baseSlotRingSize / unityBoard.baseCellRadius) * 0.5;
  ctx.beginPath();
  ctx.arc(x, y, ringRadius, 0, Math.PI * 2);
  ctx.strokeStyle = legal ? unityColors.targetRing : unityColors.slotRing;
  ctx.lineWidth = 1.7;
  ctx.stroke();

  if (legal) {
    ctx.beginPath();
    ctx.arc(x, y, ringRadius * 0.72, 0, Math.PI * 2);
    ctx.fillStyle = unityColors.targetDot;
    ctx.fill();
  }
}

function drawUnityPiece(x, y, cellRadius, color, selected, pieceKey) {
  const now = performance.now();

  // Selection pulse: matches Unity's 1.08 + sin(t*7)*0.045
  let animScale = 1.0;
  if (selected) {
    const t = now / 1000;
    animScale = 1.08 + Math.sin(t * 7) * 0.045;
  }

  // Move-arrival flash: 1.18 → 1.0 over 180ms (matches AnimatePiecePulse)
  if (pieceKey) {
    const pulse = state.piecePulses.get(pieceKey);
    if (pulse) {
      const elapsed = (now - pulse.startTime) / pulse.duration;
      if (elapsed < 1) {
        const pf = pulse.maxScale + (1 - pulse.maxScale) * elapsed;
        if (pf > animScale) animScale = pf;
      } else {
        state.piecePulses.delete(pieceKey);
      }
    }
  }

  ctx.save();
  if (animScale !== 1.0) {
    ctx.translate(x, y);
    ctx.scale(animScale, animScale);
    ctx.translate(-x, -y);
  }

  const shadowRadius = cellRadius * (unityBoard.basePieceShadowSize / unityBoard.baseCellRadius) * 0.5;
  const pieceRadius = cellRadius * (unityBoard.basePieceSize / unityBoard.baseCellRadius) * 0.5;
  const selectionRadius = cellRadius * (unityBoard.baseSelectionSize / unityBoard.baseCellRadius) * 0.5;
  const shadowOffsetX = 8 * cellRadius / unityBoard.baseCellRadius;
  const shadowOffsetY = -9 * cellRadius / unityBoard.baseCellRadius;

  ctx.beginPath();
  ctx.arc(x + shadowOffsetX, y - shadowOffsetY, shadowRadius, 0, Math.PI * 2);
  ctx.fillStyle = unityColors.shadow;
  ctx.fill();

  const gradient = ctx.createRadialGradient(x - pieceRadius * .27, y + pieceRadius * .26, 2, x, y, pieceRadius);
  gradient.addColorStop(0, "rgba(255,255,255,.82)");
  gradient.addColorStop(.2, color);
  gradient.addColorStop(1, shadeRgb(color, -38));
  ctx.beginPath();
  ctx.arc(x, y, pieceRadius, 0, Math.PI * 2);
  ctx.fillStyle = gradient;
  ctx.fill();

  if (selected) {
    drawPieceHighlight(x, y, pieceRadius, unityColors.selection);
    drawPieceHighlight(x, y, selectionRadius, unityColors.selection);
  }

  ctx.restore();
}

function drawPieceHighlight(x, y, radius, color) {
  ctx.beginPath();
  ctx.ellipse(x - radius * .27, y + radius * .24, radius * .4, radius * .3, 0, 0, Math.PI * 2);
  const highlight = ctx.createRadialGradient(x - radius * .27, y + radius * .24, 1, x - radius * .27, y + radius * .24, radius * .42);
  highlight.addColorStop(0, color);
  highlight.addColorStop(1, "rgba(255,255,255,0)");
  ctx.fillStyle = highlight;
  ctx.fill();
}

function shadeRgb(rgb, amount) {
  const parts = rgb.match(/\d+/g).map(Number);
  const r = Math.max(0, Math.min(255, parts[0] + amount));
  const g = Math.max(0, Math.min(255, parts[1] + amount));
  const b = Math.max(0, Math.min(255, parts[2] + amount));
  return `rgb(${r}, ${g}, ${b})`;
}

function mix(a, b, t) {
  return [
    a[0] + (b[0] - a[0]) * t,
    a[1] + (b[1] - a[1]) * t,
    a[2] + (b[2] - a[2]) * t
  ];
}

function clamp01(value) {
  return Math.max(0, Math.min(1, value));
}

function valueNoise(x, y) {
  const xi = Math.floor(x);
  const yi = Math.floor(y);
  const xf = x - xi;
  const yf = y - yi;
  const top = lerp(hashNoise(xi, yi), hashNoise(xi + 1, yi), smoothstep(xf));
  const bottom = lerp(hashNoise(xi, yi + 1), hashNoise(xi + 1, yi + 1), smoothstep(xf));
  return lerp(top, bottom, smoothstep(yf));
}

function hashNoise(x, y) {
  const value = Math.sin(x * 127.1 + y * 311.7) * 43758.5453123;
  return value - Math.floor(value);
}

function smoothstep(t) {
  return t * t * (3 - 2 * t);
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

function allCells() {
  const ranges = {
    "-8": [4, 4], "-7": [3, 4], "-6": [2, 4], "-5": [1, 4],
    "-4": [-4, 8], "-3": [-4, 7], "-2": [-4, 6], "-1": [-4, 5],
    "0": [-4, 4], "1": [-5, 4], "2": [-6, 4], "3": [-7, 4],
    "4": [-8, 4], "5": [-4, -1], "6": [-4, -2], "7": [-4, -3], "8": [-4, -4]
  };
  const cells = [];
  for (const [r, range] of Object.entries(ranges)) {
    for (let q = range[0]; q <= range[1]; q++) {
      cells.push({ q, r: Number(r) });
    }
  }
  return cells;
}

function coordKey(coord) {
  return `${coord.q},${coord.r}`;
}

function clickBoard(event) {
  if (!state.snapshot || state.snapshot.currentPlayerSlot !== state.mySlot || state.snapshot.currentPlayerKind !== "Human") {
    return;
  }

  const rect = els.canvas.getBoundingClientRect();
  const scaleX = els.canvas.width / rect.width;
  const scaleY = els.canvas.height / rect.height;
  const x = (event.clientX - rect.left) * scaleX;
  const y = (event.clientY - rect.top) * scaleY;
  let nearest = null;
  let nearestDistance = Infinity;
  const pts = getCachedCellPoints();
  for (const cell of cachedCells) {
    const point = pts.get(coordKey(cell));
    const distance = Math.hypot(point.x - x, point.y - y);
    if (distance < nearestDistance) {
      nearestDistance = distance;
      nearest = cell;
    }
  }

  if (!nearest || nearestDistance > getCellRadius()) {
    return;
  }

  const legal = new Set((state.snapshot.legalTargets || []).map(coordKey));
  if (legal.has(coordKey(nearest)) && state.selectedPieceId > 0) {
    send({ type: "MOVE", pieceId: state.selectedPieceId, q: nearest.q, r: nearest.r });
    return;
  }

  const piece = (state.snapshot.pieces || []).find(value => value.position.q === nearest.q && value.position.r === nearest.r);
  if (piece) {
    send({ type: "SELECT", pieceId: piece.pieceId });
  }
}

function startRenderLoop() {
  if (state.renderLoopId !== null) return;
  function tick() {
    drawBoard();
    state.renderLoopId = requestAnimationFrame(tick);
  }
  state.renderLoopId = requestAnimationFrame(tick);
}

function stopRenderLoop() {
  if (state.renderLoopId !== null) {
    cancelAnimationFrame(state.renderLoopId);
    state.renderLoopId = null;
  }
  drawBoard();
}

els.loginButton.addEventListener("click", () => {
  send({ type: "AUTH", account: els.accountInput.value.trim(), password: els.passwordInput.value });
});
let adminTapTimer = null;
document.querySelector(".brand-mark").addEventListener("click", () => {
  state.adminTapCount++;
  if (state.adminTapCount >= 5) {
    state.adminTapCount = 0;
    els.adminPanel.classList.remove("hidden");
    els.adminPasswordInput.focus();
  }
  clearTimeout(adminTapTimer);
  adminTapTimer = setTimeout(() => { state.adminTapCount = 0; }, 2000);
});
els.adminLoginButton.addEventListener("click", () => {
  send({ type: "ADMIN_AUTH", name: "管理员", password: els.adminPasswordInput.value });
});
els.adminRefreshButton.addEventListener("click", () => send({ type: "ADMIN_SNAPSHOT" }));
els.adminBackButton.addEventListener("click", () => {
  state.adminAuthed = false;
  refreshPanels();
});
els.addPlayerButton.addEventListener("click", () => {
  send({
    type: "ADD_PLAYER",
    account: els.newAccountInput.value.trim(),
    password: els.newPasswordInput.value.trim(),
    name: els.newNameInput.value.trim()
  });
});
els.saveNicknameButton.addEventListener("click", () => {
  send({ type: "UPDATE_NICKNAME", name: els.nicknameInput.value.trim() });
});
els.createButton.addEventListener("click", () => {
  if (Date.now() < state.createCooldownUntil) {
    return;
  }
  startCreateCooldown();
  send({ type: "CREATE", ruleVariant: els.ruleSelect.value });
});
els.joinButton.addEventListener("click", () => send({ type: "JOIN", roomKey: els.roomKeyInput.value.trim() }));
els.refreshButton.addEventListener("click", () => send({ type: "LIST" }));
els.startButton.addEventListener("click", () => send({ type: "START" }));
els.finishButton.addEventListener("click", () => send({ type: "FINISH" }));
els.passButton.addEventListener("click", () => send({ type: "PASS" }));
els.canvas.addEventListener("click", clickBoard);
els.accountInput.value = localStorage.getItem("sweetJumpJumpAccount") || els.accountInput.value;
refreshPanels();
drawBoard();
connect();
