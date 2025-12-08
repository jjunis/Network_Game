// server.js
const express = require('express');
const mysql = require('mysql2');
const cors = require('cors');
const bodyParser = require('body-parser');

const app = express();
app.use(cors());

app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));

// ✅ MySQL 연결
const db = mysql.createConnection({
    host: 'localhost',   // DB 주소
    user: 'root',        // MySQL 계정
    password: '2316',        // 비밀번호 (잠깐 수정함)
    database: 'devilrundb'
});

db.connect(err => {
    if (err) console.log('❌ DB 연결 실패:', err);
    else console.log('✅ MySQL 연결 성공');
});

// ✅ 회원가입
app.post('/register', (req, res) => {
    const { username, password } = req.body;

    db.query(
        'INSERT INTO users (username, password) VALUES (?, ?)',
        [username, password],
        (err, result) => {
            if (err) {
                console.log(err);
                res.json({ success: false, message: '이미 존재하거나 오류' });
            } else {
                res.json({ success: true, message: '회원가입 성공' });
            }
        }
    );
});

// ✅ 로그인
app.post('/login', (req, res) => {
    const { username, password } = req.body;

    db.query(
        'SELECT * FROM users WHERE username=? AND password=?',
        [username, password],
        (err, results) => {
            if (err) throw err;
            if (results.length > 0) {
                res.json({ success: true, message: '로그인 성공' });
            } else {
                res.json({ success: false, message: '아이디나 비밀번호가 틀림' });
            }
        }
    );
});

// [추가됨 3] 로비 및 방 관리 로직
let rooms = {}; 

// 1. 방 목록 가져오기 (GET)
app.get('/room_list', (req, res) => {
    let roomData = [];
    for(let key in rooms) {
        roomData.push({ name: key, count: rooms[key].players.length });
    }
    res.json(roomData);
});

// 2. 방 만들기 (POST)
app.post('/create_room', (req, res) => {
    const { roomName, nickName } = req.body;

    // 메모리에 이미 존재하는지 먼저 체크
    if (rooms[roomName]) {
        return res.json({ success: false, message: "이미 있는 방입니다." });
    }

    // 1) DB에 방 추가
    const insertRoomQuery = "INSERT INTO rooms (roomName) VALUES (?)";
    db.query(insertRoomQuery, [roomName], (err) => {
        if (err) {
            console.log(err);
            return res.json({ success: false, message: "방 생성 실패(DB)" });
        }

        // 2) DB에 방장 추가
        const insertPlayerQuery = `
            INSERT INTO room_players (roomName, nickName, isReady, isHost)
            VALUES (?, ?, ?, ?)
        `;
        db.query(insertPlayerQuery, [roomName, nickName, true, true], (err2) => {
            if (err2) {
                console.log(err2);
                return res.json({ success: false, message: "플레이어 저장 실패" });
            }

            // 3) Node.js 메모리에 저장 (기존 로직)
            rooms[roomName] = {
                state: 'waiting',
                players: [{ nick: nickName, isReady: true, isHost: true }]
            };

            res.json({ success: true, message: "방 생성 완료 (DB + 메모리)" });
        });
    });
});

// 3. 방 들어가기 (POST)
app.post('/join_room', (req, res) => {
    const { roomName, nickName } = req.body;

    if (!rooms[roomName]) {
        return res.json({ success: false, message: "없는 방입니다." });
    }

    if (rooms[roomName].players.length >= 3) {
        return res.json({ success: false, message: "방이 꽉 찼습니다." });
    }

    if (rooms[roomName].state !== 'waiting') {
        return res.json({ success: false, message: "게임이 시작된 방입니다." });
    }

    // DB에도 저장
    const sql = `
        INSERT INTO room_players (roomName, nickName, isReady, isHost)
        VALUES (?, ?, ?, ?)
    `;
    db.query(sql, [roomName, nickName, false, false], (err) => {
        if (err) {
            console.log(err);
            return res.json({ success: false, message: "DB 저장 실패" });
        }

        // 메모리에도 저장
        rooms[roomName].players.push({
            nick: nickName,
            isReady: false,
            isHost: false
        });

        res.json({ success: true, message: "입장 성공" });
    });
});

// ✅ 서버 실행
const PORT = 3000;
app.listen(PORT, () => {
    console.log(`🌐 HTTP 서버 실행 중: http://localhost:${PORT}`);
});
// --- [생존신고(Heartbeat) 시스템] ---

// 유저들의 마지막 생존 시간을 기록하는 장부
let lastHeartbeat = {}; 

// 4. 생존신고 받기 (Unity가 1초마다 보냄)
app.post('/ping', (req, res) => {
    const { nickName } = req.body;
    lastHeartbeat[nickName] = Date.now(); // 현재 시간 기록
    res.end();
});

// 5. 청소부 (2초마다 돌면서 죽은 유저 내쫓기)
setInterval(() => {
    const now = Date.now();
    
    for (let roomName in rooms) {

        let room = rooms[roomName];
        let players = room.players;

        for (let i = players.length - 1; i >= 0; i--) {
            let p = players[i];
            let nick = p.nick;

            if (!lastHeartbeat[nick] || (now - lastHeartbeat[nick] > 4000)) {
                console.log(`💀 [삭제] ${nick}`);

                // 🔥 DB에서도 삭제
                const sql = "DELETE FROM room_players WHERE roomName=? AND nickName=?";
                db.query(sql, [roomName, nick], () => {});

                players.splice(i, 1);
                delete lastHeartbeat[nick];
            }
        }

        // 방에 유저가 0명 → 방 삭제 (DB도 삭제)
        if (players.length === 0) {
            console.log(`🗑 [방 폭파] ${roomName}`);

            // 🔥 DB 방 삭제
            db.query("DELETE FROM rooms WHERE roomName=?", [roomName], () => {});

            // 🔥 DB room_players도 삭제
            db.query("DELETE FROM room_players WHERE roomName=?", [roomName], () => {});

            delete rooms[roomName];
        }
    }
}, 2000);

// 6. 플레이어 목록 가져오기 (GET) - 대기실 UI 갱신용
app.get('/room_players', (req, res) => {
    const { roomName } = req.query;

    if (!rooms[roomName]) {
        return res.json({ isStarted: false, players: [] });
    }
    
    // Unity 클라이언트가 원하는 JSON 구조 [{nickName: 'A', isReady: true}, ...]로 변환
    const playersForUnity = rooms[roomName].players.map(p => ({
        nickName: p.nick, 
        isReady: p.isReady
    }));

    const isGameStarted = (rooms[roomName].state === 'playing');

    res.json({
        isStarted: isGameStarted,
        players: playersForUnity
    });
});

// 7. 준비 상태 토글 (POST)
app.post('/toggle_ready', (req, res) => {
    const { roomName, nickName, isReady } = req.body;

    if (!rooms[roomName]) {
        return res.json({ success: false, message: "방이 없습니다." });
    }

    const player = rooms[roomName].players.find(p => p.nick === nickName);

    if (player && !player.isHost) {

        // DB UPDATE
        const sql = `
            UPDATE room_players 
            SET isReady = ? 
            WHERE roomName = ? AND nickName = ?
        `;
        db.query(sql, [isReady, roomName, nickName], (err) => {
            if (err) {
                console.log(err);
                return res.json({ success: false, message: "DB 업데이트 실패" });
            }

            player.isReady = isReady;
            res.json({ success: true, message: "준비 상태 갱신" });
        });

        return;
    }

    res.json({ success: false, message: "플레이어를 찾을 수 없거나 방장입니다." });
});


// 8. 게임 시작 요청 (POST) - 호스트 전용
app.post('/start_game', (req, res) => {
    const { roomName } = req.body;

    const room = rooms[roomName];

    if (!room || room.state !== 'waiting') {
        return res.json({ success: false, message: "방이 없거나 이미 시작됨." });
    }

    const players = room.players;
    const isFull = players.length === 3;
    const allReady = players.every(p => p.isReady);

    if (isFull && allReady) {

        // DB UPDATE
        const sql = "UPDATE rooms SET state = 'playing' WHERE roomName = ?";
        db.query(sql, [roomName], (err) => {
            if (err) console.log(err);
        });

        // 메모리 업데이트
        room.state = 'playing';

        console.log(`🚀 [GAME START] Room: ${roomName}`);
        return res.json({ success: true, message: "게임 시작!" });
    }

    res.json({ success: false, message: "인원 부족 또는 준비 안됨." });
});