const express = require('express');
const cors = require('cors');
const bodyParser = require('body-parser');
const path = require('path');
const mongoose = require('mongoose');

const app = express();
const PORT = process.env.PORT || 3000;

// --- CONFIGURATION ---
// PASTE YOUR CONNECTION STRING BELOW (Replace <password> with your real password)
const MONGO_URI = "mongodb+srv://ekenherli_db_user:f5hQVLp2IW42dSyS@cluster0.vx9uahh.mongodb.net/?appName=Cluster0";

app.use(cors());
app.use(bodyParser.json());
app.use(express.static(path.join(__dirname, 'public')));

// --- DATABASE CONNECTION ---
mongoose.connect(MONGO_URI)
    .then(() => console.log('Connected to MongoDB Atlas'))
    .catch(err => console.error('Could not connect to MongoDB:', err));

// --- DATA MODEL ---
// --- DATA MODEL ---
const UserSchema = new mongoose.Schema({
    telegram_id: Number,
    username: String,
    first_name: String,
    gold: { type: Number, default: 100 },
    // We save the coordinates of chunks they own (e.g., {x:0, y:0})
    owned_chunks: [{ x: Number, y: Number }], 
    // We save customized walls if they paint them later
    painted_walls: [{ x: Number, y: Number, type_id: String }],
    // Furniture objects
    objects_list: [{ x: Number, y: Number, type_id: String }]   
});
const User = mongoose.model('User', UserSchema);

// --- HELPER ---
function generateRandomSet() {
    let set = [];
    for(let i=0; i<3; i++) set.push(Math.floor(Math.random() * 100) + 1);
    return set;
}

// --- ENDPOINTS ---

// --- DEV TOOL: Reset Database ---
app.get('/reset', async (req, res) => {
    try {
        await User.deleteMany({}); // Deletes ALL documents in the User collection
        console.log("Database wiped!");
        res.send("Database has been reset. All users deleted.");
    } catch (e) {
        res.status(500).send(e.message);
    }
});

app.get('/seed', async (req, res) => {
    try {
        const fakeUsers = [
            { 
                telegram_id: 101, 
                first_name: "Alice", 
                username: "alice_shop", 
                gold: 500,
                owned_chunks: [{ x: 0, y: 0 }], // Starts with 1 room
                objects_list: []
            },
            { 
                telegram_id: 102, 
                first_name: "Bob", 
                username: "bob_builder", 
                gold: 150,
                owned_chunks: [{ x: 0, y: 0 }, { x: 1, y: 0 }], // Bob has 2 rooms
                objects_list: []
            }
        ];

        for (const user of fakeUsers) {
            const exists = await User.findOne({ telegram_id: user.telegram_id });
            if (!exists) {
                await User.create(user);
            }
        }
        
        res.send("Seed successful! Created Alice (1 room) and Bob (2 rooms).");
    } catch (e) {
        res.status(500).send(e.message);
    }
});

app.post('/login', async (req, res) => {
    const { id, first_name, username } = req.body;
    
    try {
        let user = await User.findOne({ telegram_id: id });
        
        if (!user) {
            user = await User.create({
                telegram_id: id,
                first_name: first_name,
                username: username,
                gold: 100,
                owned_chunks: [{ x: 0, y: 0 }], // Everyone starts with the center chunk
                objects_list: []
            });
            console.log(`New user created: ${username}`);
        } else {
            console.log(`User loaded: ${username}`);
        }
        res.json(user);
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

app.post('/expand', async (req, res) => {
    const { id, chunk_x, chunk_y } = req.body;
    
    try {
        // Find the user
        const user = await User.findOne({ telegram_id: id });
        if (!user) return res.status(404).json({ error: "User not found" });

        // Basic Check: User must have enough gold (e.g., 50 gold per room)
        const COST = 50;
        if (user.gold < COST) {
            return res.status(400).json({ error: "Not enough gold" });
        }

        // Check if they already own this chunk
        const alreadyOwns = user.owned_chunks.some(c => c.x === chunk_x && c.y === chunk_y);
        if (alreadyOwns) {
            return res.status(400).json({ error: "Already owned" });
        }

        // Execute Purchase
        user.gold -= COST;
        user.owned_chunks.push({ x: chunk_x, y: chunk_y });
        await user.save();

        res.json({ success: true, new_gold: user.gold });
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

app.listen(PORT, () => {
    console.log(`Server running on port ${PORT}`);
});