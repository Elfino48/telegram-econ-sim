const express = require('express');
const cors = require('cors');
const bodyParser = require('body-parser');
const path = require('path');
const mongoose = require('mongoose');

const app = express();
const PORT = process.env.PORT || 3000;

// --- CONFIGURATION ---
const MONGO_URI = "mongodb+srv://ekenherli_db_user:f5hQVLp2IW42dSyS@cluster0.vx9uahh.mongodb.net/?appName=Cluster0";

app.use(cors());
app.use(bodyParser.json());
app.use(express.static(path.join(__dirname, 'public')));

// --- DATABASE CONNECTION ---
mongoose.connect(MONGO_URI)
    .then(() => console.log('Connected to MongoDB Atlas'))
    .catch(err => console.error('Could not connect to MongoDB:', err));

// --- CONSTANTS ---
const MASTER_NAMES = [
    "Alaric", "Berin", "Cedric", "Doran", "Elric", "Faren", "Garrick", "Haldor", "Irona", "Jorah",
    "Kael", "Lorin", "Marek", "Norin", "Orin", "Perrin", "Quinn", "Roric", "Soren", "Torin",
    "Ulric", "Varin", "Willem", "Xander", "Yorick", "Zane", "Arin", "Bram", "Cale", "Dain",
    "Ewan", "Finn", "Gale", "Holt", "Ivan", "Jace", "Kian", "Liam", "Milo", "Nia",
    "Olin", "Piers", "Reed", "Seth", "Tate", "Uri", "Vane", "West", "Xylia", "Yves"
];

const SHOP_REFRESH_TIME = 10 * 60 * 1000; // 10 Minutes in MS

// --- DATA MODEL ---
const UserSchema = new mongoose.Schema({
    telegram_id: Number,
    username: String,
    first_name: String,
    gold: { type: Number, default: 500 }, // Start with 500 Gold
    owned_chunks: [{ x: Number, y: Number }], 
    painted_walls: [{ x: Number, y: Number, type_id: String }],
    
    // Objects have flexible data (for resources etc)
    objects_list: [{ x: Number, y: Number, type_id: String, data: { type: Map, of: String } }],
    
    // Hired masters
    masters_list: [{ x: Number, y: Number, name: String }], 
    
    // Hiring Shop Data
    hire_shop: {
        next_refresh: Number, // Timestamp (ms)
        candidates: [{ name: String, price: Number, index: Number }] 
    }
});
const User = mongoose.model('User', UserSchema);

// --- HELPERS ---
function generateCandidates() {
    let candidates = [];
    for(let i=0; i<3; i++) {
        const name = MASTER_NAMES[Math.floor(Math.random() * MASTER_NAMES.length)];
        // Price: 50 to 150, divisible by 5
        const price = (Math.floor(Math.random() * 21) * 5) + 50;
        
        candidates.push({ name, price, index: i });
    }
    return candidates;
}

// --- ENDPOINTS ---

app.post('/login', async (req, res) => {
    const { id, first_name, username } = req.body;
    const now = Date.now();
    
    try {
        let user = await User.findOne({ telegram_id: id });
        
        if (!user) {
            // NEW USER
            user = await User.create({
                telegram_id: id,
                first_name: first_name,
                username: username,
                gold: 500,
                owned_chunks: [{ x: 0, y: 0 }], 
                objects_list: [],
                masters_list: [],
                hire_shop: {
                    next_refresh: now + SHOP_REFRESH_TIME,
                    candidates: generateCandidates()
                }
            });
            console.log(`New user created: ${username}`);
        } else {
            // EXISTING USER: CHECK TIMING
            if (now >= user.hire_shop.next_refresh) {
                // Calculate how much time we overshot
                const timePassedSinceTarget = now - user.hire_shop.next_refresh;
                
                // Calculate how many full 10-minute cycles fit in that gap
                const cyclesMissed = Math.floor(timePassedSinceTarget / SHOP_REFRESH_TIME) + 1;
                
                // Refresh candidates
                user.hire_shop.candidates = generateCandidates();
                
                // Jump the timer forward by exact 10m intervals to keep alignment
                user.hire_shop.next_refresh += cyclesMissed * SHOP_REFRESH_TIME;
                
                await user.save();
                console.log(`Shop refreshed. Missed ${cyclesMissed} cycles.`);
            }
            console.log(`User loaded: ${username}`);
        }
        res.json(user);
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

app.get('/users', async (req, res) => {
    try {
        const users = await User.find({}, 'telegram_id username first_name');
        res.json(users);
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

app.get('/user/:id', async (req, res) => {
    try {
        const targetId = parseInt(req.params.id);
        const user = await User.findOne({ telegram_id: targetId });
        if (user) res.json(user);
        else res.status(404).json({ error: "User not found" });
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

// Seed Endpoint (Reset Logic)
app.get('/seed', async (req, res) => {
    try {
        // Just creates Alice and Bob for testing
        const now = Date.now();
        const fakeUsers = [
            { 
                telegram_id: 101, first_name: "Alice", username: "alice_shop", gold: 500,
                owned_chunks: [{ x: 0, y: 0 }], 
                objects_list: [], masters_list: [],
                hire_shop: { next_refresh: now + SHOP_REFRESH_TIME, candidates: generateCandidates() }
            },
            { 
                telegram_id: 102, first_name: "Bob", username: "bob_builder", gold: 150,
                owned_chunks: [{ x: 0, y: 0 }, { x: 1, y: 0 }], 
                objects_list: [], masters_list: [],
                hire_shop: { next_refresh: now + SHOP_REFRESH_TIME, candidates: generateCandidates() }
            }
        ];

        for (const user of fakeUsers) {
            const exists = await User.findOne({ telegram_id: user.telegram_id });
            if (!exists) {
                await User.create(user);
            }
        }
        res.send("Seed successful!");
    } catch (e) {
        res.status(500).send(e.message);
    }
});

// Reset Database Endpoint
app.get('/reset', async (req, res) => {
    try {
        await User.deleteMany({}); 
        console.log("Database wiped!");
        res.send("Database has been reset. All users deleted.");
    } catch (e) {
        res.status(500).send(e.message);
    }
});

// Cheat Endpoint (Get Rich)
app.get('/rich', async (req, res) => {
    try {
        await User.updateMany({}, { $inc: { gold: 1000 } });
        res.send("Everyone is rich now!");
    } catch (e) {
        res.status(500).send(e.message);
    }
});

// Expand Endpoint (Free for now, as requested earlier)
app.post('/expand', async (req, res) => {
    const { id, chunk_x, chunk_y } = req.body;
    
    try {
        const user = await User.findOne({ telegram_id: id });
        if (!user) return res.status(404).json({ error: "User not found" });

        // Check if already owned
        const alreadyOwns = user.owned_chunks.some(c => c.x === chunk_x && c.y === chunk_y);
        if (alreadyOwns) {
            return res.status(400).json({ error: "Already owned" });
        }

        user.owned_chunks.push({ x: chunk_x, y: chunk_y });
        await user.save();

        res.json({ success: true, new_gold: user.gold });
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

// Place Furniture Endpoint (Supports Smart Data)
app.post('/place_object', async (req, res) => {
    const { id, x, y, type_id, data } = req.body;
    
    try {
        const user = await User.findOne({ telegram_id: id });
        if (!user) return res.status(404).json({ error: "User not found" });

        user.objects_list.push({ x, y, type_id, data: data || {} });
        await user.save();

        res.json({ success: true });
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

// Hire Master Endpoint
app.post('/hire_master', async (req, res) => {
    const { id, x, y, candidate_index } = req.body;
    
    try {
        const user = await User.findOne({ telegram_id: id });
        if (!user) return res.status(404).json({ error: "User not found" });

        // 1. Find the candidate
        const candidatePos = user.hire_shop.candidates.findIndex(c => c.index === candidate_index);
        
        if (candidatePos === -1) {
            return res.status(400).json({ error: "Master not found or already hired" });
        }

        const candidate = user.hire_shop.candidates[candidatePos];

        // 2. Check Gold
        if (user.gold < candidate.price) {
            return res.status(400).json({ error: "Not enough gold" });
        }

        // 3. Transaction
        user.gold -= candidate.price;
        
        // Remove from shop
        user.hire_shop.candidates.splice(candidatePos, 1);
        
        // Add to owned masters (With Name!)
        user.masters_list.push({ x, y, name: candidate.name });
        
        await user.save();
        res.json({ success: true, new_gold: user.gold });
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

app.listen(PORT, () => {
    console.log(`Server running on port ${PORT}`);
});